using BookExchange.Domain.Users;
using FluentValidation;

namespace BookExchange.Application.Users;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(r => r.DisplayName).DisplayName();
        RuleFor(r => r.Bio).MaximumLength(UserProfile.BioMaxLength);
        RuleFor(r => r.PreferredLanguage).Language();
    }
}

public sealed class SetHomeAreaRequestValidator : AbstractValidator<SetHomeAreaRequest>
{
    public SetHomeAreaRequestValidator()
    {
        RuleFor(r => r.Label).NotEmpty().MaximumLength(UserProfile.AreaLabelMaxLength);
        RuleFor(r => r.Latitude).InclusiveBetween(-90, 90);
        RuleFor(r => r.Longitude).InclusiveBetween(-180, 180);
    }
}

public static class UserRules
{
    public static IRuleBuilderOptions<T, string> DisplayName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Must(n => n.Trim().Length is >= UserProfile.DisplayNameMinLength and <= UserProfile.DisplayNameMaxLength)
            .WithMessage($"Display name must be between {UserProfile.DisplayNameMinLength} and {UserProfile.DisplayNameMaxLength} characters.");

    public static IRuleBuilderOptions<T, string> Language<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(SupportedLanguages.IsSupported)
            .WithMessage($"Language must be one of: {string.Join(", ", SupportedLanguages.All)}.");
}
