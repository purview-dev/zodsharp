using System.Collections.Immutable;

namespace ZodSharp.Core;

/// <summary>
/// Base class for all Zod schema types.
/// Provides common functionality and implements the fluent API pattern.
/// Similar to Zod's ZodType abstract class.
/// </summary>
/// <typeparam name="TOutput">The output type after validation</typeparam>
/// <typeparam name="TInput">The input type before validation</typeparam>
public abstract class ZodType<TOutput, TInput> : IZodSchema<TOutput, TInput>, IOptionalSchema
{
	static readonly string[] EmptyPath = [];

	ImmutableArray<IValidationRule<TOutput>> _rules = [];

	/// <inheritdoc/>
	public virtual bool IsOptional => false;

	/// <inheritdoc/>
	public virtual bool ProvidesValueOnMissing => false;

	/// <summary>
	/// Gets the description of this schema.
	/// </summary>
	public string? Description { get; private set; }

	/// <summary>
	/// Validates the input value and returns a validation result.
	/// Equivalent to Zod's safeParse method.
	/// </summary>
	public ValidationResult<TOutput> Validate(TInput value)
	{
		var parseResult = ParseInternal(value);
		if (!parseResult.IsSuccess)
			return parseResult;

		var validatedValue = parseResult.Value;

		var rulesCount = _rules.Length;
		if (rulesCount == 0)
		{
			return ValidationResult<TOutput>.Success(validatedValue);
		}

		List<ValidationError>? errors = null;

		foreach (var rule in _rules)
		{
			if (!rule.IsValid(validatedValue))
			{
				errors ??= [with(rulesCount)];
				errors.Add(new ValidationError("validation_failed", rule.GetErrorMessage(validatedValue), EmptyPath));
			}
		}

		return errors != null && errors.Count > 0
			? ValidationResult<TOutput>.Failure(errors)
			: ValidationResult<TOutput>.Success(validatedValue);
	}

	/// <summary>
	/// Validates the input value asynchronously.
	/// Equivalent to Zod's safeParseAsync method.
	/// </summary>
	public ValueTask<ValidationResult<TOutput>> ValidateAsync(
		TInput value,
		CancellationToken cancellationToken = default
	) => new(Validate(value));

	/// <summary>
	/// Parses the input value to the output type.
	/// Override this method in derived classes to implement type-specific parsing.
	/// </summary>
	protected abstract ValidationResult<TOutput> ParseInternal(TInput value);

	/// <summary>
	/// Adds a validation rule to this schema.
	/// </summary>
	protected ZodType<TOutput, TInput> AddRule(IValidationRule<TOutput> rule)
	{
		_rules = _rules.Add(rule);
		return this;
	}

	/// <summary>
	/// Sets the description of this schema.
	/// Equivalent to Zod's describe method.
	/// </summary>
	public ZodType<TOutput, TInput> Describe(string description)
	{
		Description = description;
		return this;
	}

	/// <summary>
	/// Validates and returns the value, throwing an exception if validation fails.
	/// Equivalent to Zod's parse method.
	/// </summary>
	public TOutput Parse(TInput value) => Validate(value).GetValueOrThrow();

	/// <summary>
	/// Validates and returns a result without throwing.
	/// Equivalent to Zod's safeParse method.
	/// </summary>
	public ValidationResult<TOutput> SafeParse(TInput value) => Validate(value);

	/// <summary>
	/// Transforms the validated value using a function.
	/// Equivalent to Zod's transform method.
	/// Only works when TInput == TOutput (most common case).
	/// </summary>
	public Schemas.ZodTransform<TOutput, TNewOutput> Transform<TNewOutput>(Func<TOutput, TNewOutput> transform)
	{
		if (typeof(TInput) != typeof(TOutput))
		{
			throw new InvalidOperationException("Transform can only be used when input and output types are the same");
		}

		var adapter = (IZodSchema<TOutput, TOutput>)(object)this;
		return new Schemas.ZodTransform<TOutput, TNewOutput>(new RefinementAdapter<TOutput>(adapter), transform);
	}

	/// <summary>
	/// Adds a custom validation refinement.
	/// Equivalent to Zod's refine method.
	/// </summary>
	public Schemas.ZodRefinement<TOutput> Refine(Func<TOutput, bool> refinement, string? message = null)
	{
		if (typeof(TInput) != typeof(TOutput))
		{
			throw new InvalidOperationException("Refine can only be used when input and output types are the same");
		}

		var adapter = (IZodSchema<TOutput, TOutput>)(object)this;
		return new Schemas.ZodRefinement<TOutput>(new RefinementAdapter<TOutput>(adapter), refinement, message);
	}

	/// <summary>
	/// Adds a default value when input is null.
	/// Equivalent to Zod's default method.
	/// </summary>
	public Schemas.ZodDefault<TOutput> Default(TOutput defaultValue)
	{
		if (typeof(TInput) != typeof(TOutput))
		{
			throw new InvalidOperationException("Default can only be used when input and output types are the same");
		}

		var adapter = (IZodSchema<TOutput, TOutput>)(object)this;
		return new Schemas.ZodDefault<TOutput>(new RefinementAdapter<TOutput>(adapter), defaultValue);
	}

	/// <summary>
	/// Adds a context-aware custom validation refinement that may emit multiple,
	/// path-located issues. Equivalent to Zod's <c>superRefine</c> method.
	/// </summary>
	/// <param name="refinement">A callback receiving a <see cref="Schemas.RefineCtx{T}"/>.</param>
	/// <returns>A new schema that applies the refinement after the base validation.</returns>
	public Schemas.ZodSuperRefinement<TOutput> SuperRefine(Action<Schemas.RefineCtx<TOutput>> refinement)
	{
		if (typeof(TInput) != typeof(TOutput))
		{
			throw new InvalidOperationException(
				"SuperRefine can only be used when input and output types are the same"
			);
		}

		var adapter = (IZodSchema<TOutput, TOutput>)(object)this;
		return new Schemas.ZodSuperRefinement<TOutput>(new RefinementAdapter<TOutput>(adapter), refinement);
	}

	/// <summary>
	/// Pipes the output of this schema through another schema. Equivalent to
	/// Zod's <c>.pipe(target)</c> method and the foundation of validation
	/// pipelines: this schema validates the input, then <paramref name="target"/>
	/// validates the resulting value.
	/// </summary>
	/// <typeparam name="TTargetOutput">The output type of <paramref name="target"/>.</typeparam>
	/// <param name="target">The schema that validates this schema's output.</param>
	/// <returns>A new <see cref="Schemas.ZodPipe{TSource,TTarget}"/> schema.</returns>
	public Schemas.ZodPipe<TOutput, TTargetOutput> Pipe<TTargetOutput>(IZodSchema<TTargetOutput, TOutput> target)
	{
		if (typeof(TInput) != typeof(TOutput))
		{
			throw new InvalidOperationException("Pipe can only be used when input and output types are the same");
		}

		var adapter = (IZodSchema<TOutput, TOutput>)(object)this;
		return new Schemas.ZodPipe<TOutput, TTargetOutput>(new RefinementAdapter<TOutput>(adapter), target);
	}

	/// <summary>
	/// Returns a fallback value when validation fails instead of propagating
	/// errors. Equivalent to Zod's <c>.catch(fallback)</c> method.
	/// </summary>
	/// <param name="fallback">The value returned on validation failure.</param>
	/// <returns>A new <see cref="Schemas.ZodCatch{T}"/> schema.</returns>
	public Schemas.ZodCatch<TOutput> Catch(TOutput fallback)
	{
		if (typeof(TInput) != typeof(TOutput))
		{
			throw new InvalidOperationException("Catch can only be used when input and output types are the same");
		}

		var adapter = (IZodSchema<TOutput, TOutput>)(object)this;
		return new Schemas.ZodCatch<TOutput>(new RefinementAdapter<TOutput>(adapter), fallback);
	}

	/// <summary>
	/// Returns a fallback value when validation fails instead of propagating
	/// errors. Equivalent to Zod's <c>.catch(fn)</c> overload.
	/// </summary>
	/// <param name="fallbackFactory">
	/// A function producing the fallback value from the input and the errors.
	/// </param>
	/// <returns>A new <see cref="Schemas.ZodCatch{T}"/> schema.</returns>
	public Schemas.ZodCatch<TOutput> Catch(Func<TOutput, ImmutableArray<ValidationError>, TOutput> fallbackFactory)
	{
		if (typeof(TInput) != typeof(TOutput))
		{
			throw new InvalidOperationException("Catch can only be used when input and output types are the same");
		}

		var adapter = (IZodSchema<TOutput, TOutput>)(object)this;
		return new Schemas.ZodCatch<TOutput>(new RefinementAdapter<TOutput>(adapter), fallbackFactory);
	}

	/// <summary>
	/// Substitutes <paramref name="prefaultValue"/> when the input is the default
	/// for <typeparamref name="TOutput"/>, then validates the result. Equivalent
	/// to Zod's <c>.prefault(value)</c> method. Unlike <see cref="Default"/>, the
	/// substituted value is still validated by this schema.
	/// </summary>
	/// <param name="prefaultValue">The value used when the input is default.</param>
	/// <returns>A new <see cref="Schemas.ZodPrefault{T}"/> schema.</returns>
	public Schemas.ZodPrefault<TOutput> Prefault(TOutput prefaultValue)
	{
		if (typeof(TInput) != typeof(TOutput))
		{
			throw new InvalidOperationException("Prefault can only be used when input and output types are the same");
		}

		var adapter = (IZodSchema<TOutput, TOutput>)(object)this;
		return new Schemas.ZodPrefault<TOutput>(new RefinementAdapter<TOutput>(adapter), prefaultValue);
	}

	/// <summary>
	/// Requires both this schema and <paramref name="other"/> to pass (intersection).
	/// Equivalent to Zod's <c>.and(other)</c> method. Returns a new composable schema.
	/// </summary>
	/// <param name="other">The other schema that must also pass.</param>
	/// <returns>A new <see cref="Schemas.ZodIntersection{TOutput}"/> schema.</returns>
	public Schemas.ZodIntersection<TOutput> And(IZodSchema<TOutput, TOutput> other)
	{
		if (typeof(TInput) != typeof(TOutput))
		{
			throw new InvalidOperationException("And can only be used when input and output types are the same");
		}

		var adapter = (IZodSchema<TOutput, TOutput>)(object)this;
		return new Schemas.ZodIntersection<TOutput>(new RefinementAdapter<TOutput>(adapter), other);
	}

	/// <summary>
	/// Accepts the value if either this schema or <paramref name="other"/> passes.
	/// Equivalent to Zod's <c>.or(other)</c> method. Returns a new composable schema
	/// that yields a typed <see cref="Unions.Union{T1,T2}"/>.
	/// </summary>
	/// <typeparam name="TOther">The other option's output type.</typeparam>
	/// <param name="other">The alternative schema.</param>
	/// <returns>A new <see cref="Schemas.ZodTypedUnion{TOutput,TOther}"/> schema.</returns>
	public Schemas.ZodTypedUnion<TOutput, TOther> Or<TOther>(IZodSchema<TOther, TOther> other)
	{
		if (typeof(TInput) != typeof(TOutput))
		{
			throw new InvalidOperationException("Or can only be used when input and output types are the same");
		}

		var adapter = (IZodSchema<TOutput, TOutput>)(object)this;
		return new Schemas.ZodTypedUnion<TOutput, TOther>(new RefinementAdapter<TOutput>(adapter), other);
	}

	sealed class TransformInputAdapter<TAdapterInput, TAdapterOutput>(IZodSchema<TAdapterOutput, TAdapterInput> inner)
		: IZodSchema<TAdapterOutput, TAdapterInput>
	{
		public ValidationResult<TAdapterOutput> Validate(TAdapterInput value) => inner.Validate(value);

		public ValueTask<ValidationResult<TAdapterOutput>> ValidateAsync(
			TAdapterInput value,
			CancellationToken cancellationToken = default
		) => inner.ValidateAsync(value, cancellationToken);
	}

	sealed class RefinementAdapter<TAdapterType>(IZodSchema<TAdapterType, TAdapterType> inner)
		: IZodSchema<TAdapterType>
	{
		public ValidationResult<TAdapterType> Validate(TAdapterType value) => inner.Validate(value);

		public ValueTask<ValidationResult<TAdapterType>> ValidateAsync(
			TAdapterType value,
			CancellationToken cancellationToken = default
		) => inner.ValidateAsync(value, cancellationToken);
	}
}

/// <summary>
/// Convenience base class for schemas where input and output are the same type.
/// </summary>
/// <typeparam name="T">The type</typeparam>
public abstract class ZodType<T> : ZodType<T, T>, IZodSchema<T>, IZodSchemaValidator<T> { }
