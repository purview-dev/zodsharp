using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using ZodSharp.Core;

namespace ZodSharp.Blazor;

/// <summary>
/// Displays the validation messages of one field - a drop-in replacement for <c>ValidationMessage&lt;TValue&gt;</c>
/// with the same markup and CSS classes, plus <c>data-code</c> and <c>data-category</c> attributes when the full
/// ZodSharp error is known (<see cref="ZodValidator"/>, or server errors mapped with <c>AddZodProblemDetails</c>).
/// </summary>
/// <typeparam name="TValue">The field type.</typeparam>
public class ZodValidationMessage<TValue> : ComponentBase, IDisposable
{
	readonly EventHandler<ValidationStateChangedEventArgs> _validationStateChangedHandler;
	EditContext? _previousEditContext;
	Expression<Func<TValue>>? _previousFieldAccessor;
	FieldIdentifier _fieldIdentifier;
	string? _fieldName;

	/// <summary>
	/// Creates the component.
	/// </summary>
	public ZodValidationMessage() => _validationStateChangedHandler = (_, _) => StateHasChanged();

	[CascadingParameter]
	EditContext CurrentEditContext { get; set; } = default!;

	/// <summary>
	/// Gets or sets the attributes applied to each message element.
	/// </summary>
	[Parameter(CaptureUnmatchedValues = true)]
	public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

	/// <summary>
	/// Gets or sets the field to display messages for.
	/// </summary>
	[Parameter]
	public Expression<Func<TValue>>? For { get; set; }

	/// <summary>
	/// Gets or sets whether the error code is shown after the message, in a <c>span.validation-code</c>.
	/// </summary>
	[Parameter]
	public bool ShowCode { get; set; }

	/// <summary>
	/// Gets or sets the content rendered inside the message element for a message whose full error is known. Messages
	/// without one (<c>DataAnnotationsValidator</c>) render as text.
	/// </summary>
	[Parameter]
	public RenderFragment<ValidationError>? Template { get; set; }

	/// <inheritdoc/>
	protected override void OnParametersSet()
	{
		if (CurrentEditContext is null)
			throw new InvalidOperationException(
				$"{GetType()} requires a cascading parameter of type {nameof(EditContext)}. For example, you can use "
					+ $"{GetType()} inside an {nameof(EditForm)}."
			);

		if (For is null)
			throw new InvalidOperationException($"{GetType()} requires a value for the {nameof(For)} parameter.");

		if (For != _previousFieldAccessor)
		{
			_fieldIdentifier = FieldIdentifier.Create(For);
			_fieldName = null;
			_previousFieldAccessor = For;
		}

		if (CurrentEditContext != _previousEditContext)
		{
			Detach();
			CurrentEditContext.OnValidationStateChanged += _validationStateChangedHandler;
			_previousEditContext = CurrentEditContext;
		}
	}

	/// <inheritdoc/>
	protected override void BuildRenderTree(RenderTreeBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var entries = ZodFormErrors.Get(CurrentEditContext)?.GetEntries(_fieldIdentifier) ?? [];
		var isStatic = AssignedRenderMode is null;
		var first = true;
		foreach (var message in CurrentEditContext.GetValidationMessages(_fieldIdentifier))
		{
			var error = ZodMessageMatcher.Take(entries, message);
			builder.OpenElement(0, "div");
			builder.AddAttribute(1, "class", "validation-message");
			if (isStatic && first)
			{
				builder.AddAttribute(2, "data-valmsg-for", GetFieldName());
				builder.AddAttribute(3, "data-valmsg-replace", "true");
			}

			first = false;
			if (error is { } known)
			{
				builder.AddAttribute(4, "data-code", known.Code);
				if (known.Category is { Length: > 0 } category)
					builder.AddAttribute(5, "data-category", category);
			}

			builder.AddMultipleAttributes(6, AdditionalAttributes);
			if (error is { } templated && Template is not null)
			{
				builder.AddContent(7, Template(templated));
			}
			else
			{
				builder.AddContent(8, message);
				if (ShowCode && error is { } coded)
				{
					builder.OpenElement(9, "span");
					builder.AddAttribute(10, "class", "validation-code");
					builder.AddContent(11, coded.Code);
					builder.CloseElement();
				}
			}

			builder.CloseElement();
		}

		// Static SSR: an empty placeholder the client-side validator fills in, exactly as ValidationMessage renders.
		if (isStatic && first)
		{
			builder.OpenElement(12, "div");
			builder.AddAttribute(13, "class", "validation-message");
			builder.AddAttribute(14, "data-valmsg-for", GetFieldName());
			builder.AddAttribute(15, "data-valmsg-replace", "true");
			builder.AddAttribute(16, "hidden", true);
			builder.AddMultipleAttributes(17, AdditionalAttributes);
			builder.CloseElement();
		}
	}

	string GetFieldName() => _fieldName ??= ZodFieldNameFormatter.Format(For!);

	/// <summary>
	/// Releases resources.
	/// </summary>
	/// <param name="disposing">Whether managed resources are released.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
			Detach();
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	void Detach()
	{
		if (_previousEditContext is not null)
			_previousEditContext.OnValidationStateChanged -= _validationStateChangedHandler;
	}
}

/// <summary>
/// Pairs rendered messages with the full errors behind them.
/// </summary>
static class ZodMessageMatcher
{
	/// <summary>
	/// Takes the first entry shown with <paramref name="message"/>, so duplicate messages pair one-to-one.
	/// </summary>
	public static ValidationError? Take(List<ZodFieldError> entries, string message)
	{
		for (var i = 0; i < entries.Count; i++)
		{
			if (string.Equals(entries[i].Message, message, StringComparison.Ordinal))
			{
				var error = entries[i].Error;
				entries.RemoveAt(i);
				return error;
			}
		}

		return null;
	}
}
