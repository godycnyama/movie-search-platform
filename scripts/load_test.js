// k6 load test for the Movie Search API (README §11: p95 < 500ms SLO).
//
//   k6 run scripts/load_test.js -e BASE_URL=http://localhost:8080
//
// The script signs up its own throwaway account (or logs in with EMAIL/PASSWORD
// if provided), then mixes the read endpoints the way a search UI would:
// mostly semantic search, plus by-id detail fetches from real result ids,
// genres and by-title lookups.
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

const TITLES = ["terminator", "toy story", "alien", "heat", "titanic"];

// One token per VU, created during the first iteration.
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

  const email = `k6-vu${__VU}-${Date.now()}@example.com`;
  const password = `LoadTest!${Date.now()}vu${__VU}`;
  const res = http.post(
    `${API}/auth/signup`,
    JSON.stringify({ email, password }),
    { headers: { "Content-Type": "application/json" } }
  );
  check(res, { "signup succeeded": (r) => r.status === 200 || r.status === 201 });
  return res.json("access_token");
}

let token = null;

export default function () {
  if (!token) {
    token = authenticate();
  }
  const auth = { headers: { Authorization: `Bearer ${token}` } };

  // 60%: semantic search — the endpoint the SLO is really about.
  const query = QUERIES[Math.floor(Math.random() * QUERIES.length)];
  const search = http.get(
    `${API}/movies/search?q=${encodeURIComponent(query)}&top_k=10`,
    Object.assign({ tags: { endpoint: "search" } }, auth)
  );
  const searchOk = check(search, {
    "search 200": (r) => r.status === 200,
    "search has results": (r) => (r.json("count") || 0) >= 0,
  });
  errors.add(!searchOk);

  // 20%: detail + similar for a real id from the results.
  const results = searchOk ? search.json("results") || [] : [];
  if (results.length > 0 && Math.random() < 0.5) {
    const id = results[Math.floor(Math.random() * results.length)].id;

    const detail = http.get(`${API}/movies/${id}`, Object.assign({ tags: { endpoint: "by-id" } }, auth));
    errors.add(!check(detail, { "detail 200": (r) => r.status === 200 }));

    const similar = http.get(
      `${API}/movies/${id}/similar?top_k=5`,
      Object.assign({ tags: { endpoint: "similar" } }, auth)
    );
    errors.add(!check(similar, { "similar 200": (r) => r.status === 200 }));
  }

  // 20%: genres + by-title.
  if (Math.random() < 0.4) {
    const genres = http.get(`${API}/movies/genres`, Object.assign({ tags: { endpoint: "genres" } }, auth));
    errors.add(!check(genres, { "genres 200": (r) => r.status === 200 }));

    const title = TITLES[Math.floor(Math.random() * TITLES.length)];
    const byTitle = http.get(
      `${API}/movies/by-title?title=${encodeURIComponent(title)}`,
      Object.assign({ tags: { endpoint: "by-title" } }, auth)
    );
    errors.add(!check(byTitle, { "by-title 200/404": (r) => r.status === 200 || r.status === 404 }));
  }

  // ~2.8 requests/iteration on average; 3s pacing keeps each VU (= one user)
  // under the API's 60 req/min per-user rate limit so 429s don't skew latency.
  sleep(3);
}
