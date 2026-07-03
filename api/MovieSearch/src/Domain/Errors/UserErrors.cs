namespace Domain.Errors;

public static class UserErrors
{
    public static Error EmailTaken(string email) => new(
        "User.EmailTaken", $"An account already exists for '{email}'");

    /// <summary>Deliberately identical for unknown email and wrong password, so responses don't leak which accounts exist.</summary>
    public static Error InvalidCredentials() => new(
        "User.InvalidCredentials", "The supplied email or password is incorrect");

    public static Error NotFound(Guid id) => new(
        "User.NotFound", $"User with id '{id}' does not exist");

    public static Error PasswordIncorrect() => new(
        "User.PasswordIncorrect", "The current password is incorrect");
}
