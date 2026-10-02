using BookExchange.Application.Books;
using BookExchange.Domain.Books;
using BookExchange.Domain.Listings;
using FluentValidation;

namespace BookExchange.Application.Listings;

public sealed class CreateListingRequestValidator : AbstractValidator<CreateListingRequest>
{
    public CreateListingRequestValidator()
    {
        RuleFor(r => r).Must(r => (r.BookId is not null) ^ (r.ManualBook is not null))
            .WithName("book").WithMessage("Give either bookId or manualBook.");
        RuleFor(r => r.ManualBook!).SetValidator(new ManualBookRequestValidator()).When(r => r.ManualBook is not null);
        RuleFor(r => r.Condition).IsInEnum();
        RuleFor(r => r.Category).NotNull().IsInEnum();
        RuleFor(r => r.Description).MaximumLength(Listing.DescriptionMaxLength);
        RuleFor(r => r.AreaLabel).MaximumLength(Listing.AreaLabelMaxLength);
        Include(new CoordinatesValidator<CreateListingRequest>(r => r.Latitude, r => r.Longitude));
    }
}

public sealed class UpdateListingRequestValidator : AbstractValidator<UpdateListingRequest>
{
    public UpdateListingRequestValidator()
    {
        RuleFor(r => r.Condition).IsInEnum();
        RuleFor(r => r.Category).NotNull().IsInEnum();
        RuleFor(r => r.Description).MaximumLength(Listing.DescriptionMaxLength);
        RuleFor(r => r.AreaLabel).MaximumLength(Listing.AreaLabelMaxLength);
        Include(new CoordinatesValidator<UpdateListingRequest>(r => r.Latitude, r => r.Longitude));
    }
}

public sealed class ManualBookRequestValidator : AbstractValidator<ManualBookRequest>
{
    public ManualBookRequestValidator()
    {
        RuleFor(b => b.Title).NotEmpty().MaximumLength(Book.TitleMaxLength);
        RuleFor(b => b.Authors).NotNull().Must(a => a.Count <= Book.MaxAuthors)
            .WithMessage($"At most {Book.MaxAuthors} authors.");
        RuleForEach(b => b.Authors).MaximumLength(Book.AuthorMaxLength);
        RuleFor(b => b.Isbn).Must(i => Isbn.Normalize(i) is not null)
            .When(b => !string.IsNullOrWhiteSpace(b.Isbn)).WithMessage("That is not a valid ISBN.");
    }
}

public sealed class ListingSearchRequestValidator : AbstractValidator<ListingSearchRequest>
{
    public ListingSearchRequestValidator()
    {
        Include(new CoordinatesValidator<ListingSearchRequest>(r => r.Lat, r => r.Lng, "lat", "lng"));
        RuleFor(r => r.RadiusKm).InclusiveBetween(0.5, ListingSearchLimits.MaxRadiusKm);
        RuleFor(r => r.Q).MaximumLength(200);
        RuleFor(r => r.Isbn).Must(i => Isbn.Normalize(i) is not null)
            .When(r => !string.IsNullOrWhiteSpace(r.Isbn)).WithMessage("That is not a valid ISBN.");
        RuleFor(r => r.Condition).IsInEnum();
        RuleFor(r => r.Category).IsInEnum();
        RuleFor(r => r.Sort).IsInEnum();
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1);
        RuleFor(r => r.PageSize).GreaterThanOrEqualTo(1);
    }
}

/// <summary>Latitude and longitude come together, within WGS 84 ranges.</summary>
internal sealed class CoordinatesValidator<T> : AbstractValidator<T>
{
    public CoordinatesValidator(Func<T, double?> latitude, Func<T, double?> longitude, string latitudeName = "latitude", string longitudeName = "longitude")
    {
        RuleFor(r => latitude(r)).InclusiveBetween(-90, 90).OverridePropertyName(latitudeName);
        RuleFor(r => longitude(r)).InclusiveBetween(-180, 180).OverridePropertyName(longitudeName);
        RuleFor(r => r).Must(r => latitude(r).HasValue == longitude(r).HasValue)
            .OverridePropertyName(latitudeName).WithMessage("Latitude and longitude go together.");
    }
}
