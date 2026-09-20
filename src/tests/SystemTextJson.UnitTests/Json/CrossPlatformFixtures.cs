using ZodSharp.Core;

namespace ZodSharp.Json;

/// <summary>
/// Shared cross-platform user model — mirrors the TS UserSchema (z.object).
/// Used by both Newtonsoft and System.Text.Json cross-platform tests.
/// </summary>
public sealed class CrossPlatformUser
{
	public string? Name { get; set; }

	public int Age { get; set; }

	public string? Email { get; set; }

	public List<string> Tags { get; set; } = [];
}

/// <summary>
/// Schema for CrossPlatformUser — mirrors the TS Zod UserSchema.
/// name: min 1, age: int 0-120, email: optional email, tags: string array.
/// </summary>
public sealed class CrossPlatformUserSchema : IZodSchema<CrossPlatformUser, CrossPlatformUser>
{
	static readonly string[] EmptyPath = [];

	public ValidationResult<CrossPlatformUser> Validate(CrossPlatformUser value)
	{
		if (value is null)
		{
			return ValidationResult<CrossPlatformUser>.Failure(
				new ValidationError("invalid_type", "Expected object, but got null", EmptyPath)
			);
		}

		List<ValidationError> errors = [];

		if (string.IsNullOrEmpty(value.Name) || value.Name.Trim().Length == 0)
		{
			errors.Add(new ValidationError("too_small", "name must be at least 1 character", ["name"]));
		}

		if (value.Age < 0)
		{
			errors.Add(new ValidationError("too_small", "age must be at least 0", ["age"]));
		}

		if (value.Age > 120)
		{
			errors.Add(new ValidationError("too_big", "age must be at most 120", ["age"]));
		}

		if (value.Email != null && !IsValidEmail(value.Email))
		{
			errors.Add(new ValidationError("invalid_string", "email must be a valid email", ["email"]));
		}

		if (value.Tags == null)
		{
			errors.Add(new ValidationError("invalid_type", "tags must be an array", ["tags"]));
		}

		return errors.Count == 0
			? ValidationResult<CrossPlatformUser>.Success(value)
			: ValidationResult<CrossPlatformUser>.Failure(errors);
	}

	public ValueTask<ValidationResult<CrossPlatformUser>> ValidateAsync(
		CrossPlatformUser value,
		CancellationToken cancellationToken = default
	) => new(Validate(value));

	static bool IsValidEmail(string email) =>
		email.Contains('@', StringComparison.Ordinal) && email.Contains('.', StringComparison.Ordinal);
}

/// <summary>
/// Manifest entry matching the TS manifest.json shape.
/// </summary>
public sealed class CrossPlatformManifestEntry
{
	public bool Valid { get; set; }
}

/// <summary>
/// Manifest file matching the TS manifest.json shape: { "fixtureName": { "valid": bool } }
/// </summary>
public sealed class CrossPlatformManifest : Dictionary<string, CrossPlatformManifestEntry> { }
