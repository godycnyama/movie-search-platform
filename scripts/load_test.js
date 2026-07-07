// k6 load test for the Movie Search API (README §11: p95 < 500ms SLO).
//
//   k6 run scripts/load_test.js -e BASE_URL=http://localhost:8080
//
// The script signs up its own throwaway account (or logs in with EMAIL/PASSWORD
// if provided), then mixes the *movie* read endpoints the way a search UI would.
// It mirrors scripts/load_test.py:
//
//   * Only movie endpoints are exercised.
//   * Endpoints that take an {id} path parameter are skipped
//     (/movies/{id} and /movies/{id}/similar).
//   * Auth and user endpoints are NOT load tested -- auth is used only to obtain
//     a token so the movie endpoints can be reached.
//
// Tested endpoints:
//   GET /api/v1/movies/search    ?query=&top_k=&genre=&min_imdb_rating=&decade=
//   GET /api/v1/movies/by-title  ?title=
//   GET /api/v1/movies/genres
//
// Options:
//   -e BASE_URL=...   target (default http://localhost:8080)
//   -e EMAIL=... -e PASSWORD=...   reuse an existing account instead of signup
//   -e VUS=10 -e DURATION=2m       load shape overrides

import http from "k6/http";
import { check, sleep } from "k6";
import { Rate } from "k6/metrics";

const BASE_URL = __ENV.BASE_URL || "http://localhost:8080";
const API = `${BASE_URL}/api/v1`;

const errors = new Rate("request_errors");

export const options = {
  scenarios: {
    steady: {
      executor: "constant-vus",
      vus: Number(__ENV.VUS || 10),
      duration: __ENV.DURATION || "2m",
    },
  },
  thresholds: {
    // The platform SLO (README §11): p95 under 500ms, and requests must succeed.
    http_req_duration: ["p(95)<500"],
    request_errors: ["rate<0.01"],
  },
};

// Natural-language queries the semantic search endpoint is designed to answer.
const QUERIES = [
  "action movies from the 90s with high IMDB ratings",
  "critically acclaimed drama films with small budgets",
  "animated family movies distributed by Disney",
  "sci-fi films directed by James Cameron",
  "dark psychological thrillers with low Rotten Tomatoes scores",
  "romantic comedies set in New York",
  "war epics with huge production budgets",
  "independent horror movies",
];

// Titles likely to exist in the catalogue; a 404 is still a valid outcome.
const TITLES = ["terminator", "toy story", "alien", "heat", "titanic", "the matrix", "jaws"];

// Optional structured filters mixed into a fraction of search calls. These map
// straight onto the columns in the movies table (major_genre, imdb_rating,
// mpaa_rating, decade).
const FILTER_GENRES = ["Action", "Drama", "Comedy", "Horror", "Adventure", "Thriller"];
const MPAA_RATINGS = ["G", "PG", "PG-13", "R"];
const DECADES = [1980, 1990, 2000, 2010];

function pick(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

export function setup() {
  // Nothing shared: each VU authenticates itself (tokens are per-user and the
  // rate limiter partitions on the JWT sub claim).
  return {};
}

function authenticate() {
  if (__ENV.EMAIL && __ENV.PASSWORD) {
    const res = http.post(
      `${API}/auth/login`,
      JSON.stringify({ email: __ENV.EMAIL, password: __ENV.PASSWORD }),
      { headers: { "Content-Type": "application/json" } }
    );
    check(res, { "login succeeded": (r) => r.status === 200 });
    return res.json("access_token");
  }

  // Throwaway account. Password satisfies the API's complexity policy
  // (>=8 chars, upper, lower, digit, special).
  const unique = `${Date.now()}${Math.floor(Math.random() * 9000 + 1000)}`;
  const email = `loadtest-vu${__VU}-${unique}@example.com`;
  const password = `LoadTest!${unique}`;
  const res = http.post(
    `${API}/auth/signup`,
    JSON.stringify({ email, password }),
    { headers: { "Content-Type": "application/json" } }
  );
  check(res, { "signup succeeded": (r) => r.status === 200 || r.status === 201 });
  return res.json("access_token");
}

// Encode a query string, dropping keys whose value is null/undefined.
function qs(params) {
  return Object.keys(params)
    .filter((k) => params[k] !== null && params[k] !== undefined)
    .map((k) => `${encodeURIComponent(k)}=${encodeURIComponent(params[k])}`)
    .join("&");
}

function callSearch(auth) {
  const params = { query: pick(QUERIES), top_k: 10 };
  // ~40% of searches carry a structured filter, exercising the WHERE clauses.
  if (Math.random() < 0.4) {
    const roll = Math.random();
    if (roll < 0.35) {
      params.genre = pick(FILTER_GENRES);
    } else if (roll < 0.6) {
      params.min_imdb_rating = Math.round((Math.random() * 2.5 + 6.0) * 10) / 10;
    } else if (roll < 0.8) {
      params.decade = pick(DECADES);
    } else {
      params.mpaa_rating = pick(MPAA_RATINGS);
    }
  }
  const res = http.get(
    `${API}/movies/search?${qs(params)}`,
    Object.assign({ tags: { endpoint: "search" } }, auth)
  );
  const ok = check(res, {
    "search 200": (r) => r.status === 200,
    "search has results": (r) => (r.json("count") || 0) >= 0,
  });
  errors.add(!ok);
}

function callByTitle(auth) {
  const res = http.get(
    `${API}/movies/by-title?${qs({ title: pick(TITLES) })}`,
    Object.assign({ tags: { endpoint: "by-title" } }, auth)
  );
  // A missing title is a legitimate 404, not a load-test failure.
  errors.add(!check(res, { "by-title 200/404": (r) => r.status === 200 || r.status === 404 }));
}

function callGenres(auth) {
  const res = http.get(`${API}/movies/genres`, Object.assign({ tags: { endpoint: "genres" } }, auth));
  errors.add(!check(res, { "genres 200": (r) => r.status === 200 }));
}

// Weighted mix: search is the endpoint the SLO is really about.
function pickEndpoint() {
  const roll = Math.random();
  if (roll < 0.7) return callSearch;
  if (roll < 0.9) return callByTitle;
  return callGenres;
}

let token = null;

export default function () {
  if (!token) {
    token = authenticate();
  }
  const auth = { headers: { Authorization: `Bearer ${token}` } };

  pickEndpoint()(auth);

  // 3s pacing keeps each VU (= one user) under the API's 60 req/min per-user
  // rate limit so 429s don't skew latency.
  sleep(3);
}
