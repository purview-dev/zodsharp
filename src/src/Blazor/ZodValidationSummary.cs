using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using ZodSharp.Core;

namespace ZodSharp.Blazor;

/// <summary>
/// Displays a summary of the form's validation messages - a drop-in replacement for <c>ValidationSummary</c> with the
/// same markup and CSS classes, plus <c>data-code</c>/<c>data-category</c> attributes, optional grouping by path and
/// optional union details when the full ZodSharp errors are known.
/// </summary>
public class ZodValidationSummary : ComponentBase, IDisposable
{
	readonly EventHandler<ValidationStateChangedEventArgs> _validationStateChangedHandler;
	EditContext? _previousEditContext;

	/// <summary>
	/// Creates the component.
	/// </summary>
	public ZodValidationSummary() => _validationStateChangedHandler = (_, _) => StateHasChanged();

	[CascadingParameter]
	EditContext CurrentEditContext { get; set; } = default!;

	/// <summary>
	/// Gets or sets the model whose model-level messages (those not attached to a field) are shown. When
	/// <see langword="null"/>, every message on the form is shown. Same as <c>ValidationSummary.Model</c>.
	/// </summary>
	[Parameter]
	public object? Model { get; set; }

	/// <summary>
	/// Gets or sets whether only the form model's model-level messages are shown (as when <see cref="Model"/> is the
	/// form model).
	/// </summary>
	[Parameter]
	public bool ShowModelOnly { get; set; }

	/// <summary>
	/// Gets or sets whether messages are grouped by their dotted path (<c>Lines[0].Quantity</c>). Messages without a
	/// known path are listed after the groups.
	/// </summary>
	[Parameter]
	public bool GroupByPath { get; set; }

	/// <summary>
	/// Gets or sets whether a failed union lists the issues of each option below its message.
	/// </summary>
	[Parameter]
	public bool ShowUnionDetails { get; set; }

	/// <summary>
	/// Gets or sets the attributes applied to the list element.
	/// </summary>
	[Parameter(CaptureUnmatchedValues = true)]
	public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

	/// <inheritdoc/>
	protected override void OnParametersSet()
	{
		if (CurrentEditContext is null)
			throw new InvalidOperationException(
				$"{nameof(ZodValidationSummary)} requires a cascading parameter of type {nameof(EditContext)}. For "
					+ $"example, you can use {nameof(ZodValidationSummary)} inside an {nameof(EditForm)}."
			);

		if (CurrentEditContext != _previousEditContext)
		{
			Detach();
			CurrentEditContext.OnValidationStateChanged += _validationStateChangedHandler;
			_previousEditContext = CurrentEditContext;
		}
	}

	/// <summary>
	/// One summary line.
	/// </summary>
	readonly record struct Line(string Message, ValidationError? Error, string? Path);

	List<Line> GetLines()
	{
		var errors = ZodFormErrors.Get(CurrentEditContext);
		List<Line> lines = [];

		var model = ShowModelOnly ? CurrentEditContext.Model : Model;
		if (model is not null)
		{
			FieldIdentifier field = new(model, string.Empty);
			var entries = errors?.GetEntries(field) ?? [];
			foreach (var message in CurrentEditContext.GetValidationMessages(field))
				lines.Add(ToLine(message, ZodMessageMatcher.Take(entries, message)));

			return lines;
		}

		// Pair every message with its error by message text.
		var all = errors?.GetAllEntries() ?? [];
		List<ZodFieldError> pool = [.. all.Select(static e => e.Entry)];
		foreach (var message in CurrentEditContext.GetValidationMessages())
			lines.Add(ToLine(message, ZodMessageMatcher.Take(pool, message)));

		return lines;
	}

	static Line ToLine(string message, ValidationError? error) =>
		new(message, error, error is { } known ? known.FullPath.ToDottedString() : null);

	/// <inheritdoc/>
	protected override void BuildRenderTree(RenderTreeBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var lines = GetLines();
		var isStatic = AssignedRenderMode is null;
		var hasMessages = lines.Count > 0;

		if (!isStatic && !hasMessages)
			return;

		builder.OpenElement(0, "ul");
		builder.AddAttribute(
			1,
			"class",
			isStatic
				? hasMessages
					? "validation-errors validation-summary-errors"
					: "validation-errors validation-summary-valid"
				: "validation-errors"
		);
		builder.AddMultipleAttributes(2, AdditionalAttributes);
		if (isStatic)
		{
			builder.AddAttribute(3, "data-valmsg-summary", "true");
			if (!hasMessages)
				builder.AddAttribute(4, "hidden", true);
		}

		if (GroupByPath)
		{
			foreach (var group in lines.Where(static l => l.Path is not null).GroupBy(static l => l.Path))
			{
				builder.OpenElement(5, "li");
				builder.AddAttribute(6, "class", "validation-group");
				builder.AddAttribute(7, "data-path", group.Key);
				builder.OpenElement(8, "span");
				builder.AddAttribute(9, "class", "validation-path");
				builder.AddContent(10, group.Key);
				builder.CloseElement();
				builder.OpenElement(11, "ul");
				foreach (var line in group)
					RenderLine(builder, line);

				builder.CloseElement();
				builder.CloseElement();
			}

			foreach (var line in lines.Where(static l => l.Path is null))
				RenderLine(builder, line);
		}
		else
		{
			foreach (var line in lines)
				RenderLine(builder, line);
		}

		builder.CloseElement();
	}

	void RenderLine(RenderTreeBuilder builder, in Line line)
	{
		builder.OpenElement(20, "li");
		builder.AddAttribute(21, "class", "validation-message");
		if (line.Error is { } error)
		{
			builder.AddAttribute(22, "data-code", error.Code);
			if (error.Category is { Length: > 0 } category)
				builder.AddAttribute(23, "data-category", category);
		}

		builder.AddContent(24, line.Message);

		if (ShowUnionDetails && line.Error is { } union && union.GetUnionErrors() is { Length: > 0 } options)
		{
			builder.OpenElement(25, "ol");
			builder.AddAttribute(26, "class", "validation-union");
			foreach (var option in options)
			{
				builder.OpenElement(27, "li");
				builder.AddAttribute(28, "class", "validation-union-option");
				builder.OpenElement(29, "ul");
				foreach (var issue in option)
				{
					builder.OpenElement(30, "li");
					builder.AddAttribute(31, "class", "validation-message");
					builder.AddAttribute(32, "data-code", issue.Code);
					builder.AddAttribute(33, "data-path", issue.FullPath.ToDottedString());
					builder.AddContent(34, issue.Message);
					builder.CloseElement();
				}

				builder.CloseElement();
				builder.CloseElement();
			}

			builder.CloseElement();
		}

		builder.CloseElement();
	}

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
