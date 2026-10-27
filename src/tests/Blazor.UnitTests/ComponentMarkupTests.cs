using System.Linq.Expressions;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using ZodSharp.Blazor.Fixtures;
using ZodSharp.Core;
using static ZodSharp.Blazor.Fixtures.Errors;

namespace ZodSharp.Blazor;

/// <summary>
/// Markup parity of <see cref="ZodValidationMessage{TValue}"/> and <see cref="ZodValidationSummary"/> with the built-in
/// components, plus their ZodSharp additions.
/// </summary>
public class ComponentMarkupTests
{
	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task ZodValidationMessage_GivenPlainMessages_RendersTheSameMarkupAsValidationMessage(bool interactive)
	{
		// Arrange
		Order order = new();
		await using var context = FormHost.CreateContext();
		EditContext editContext = new(order);
		ValidationMessageStore store = new(editContext);
		store.Add(editContext.Field(nameof(Order.Reference)), "Reference is required");
		store.Add(editContext.Field(nameof(Order.Reference)), "Reference is too short");

		// Act
		var builtIn = RenderMessage<ValidationMessage<string?>, string?>(
			context,
			editContext,
			() => order.Reference,
			interactive
		);
		var zod = RenderMessage<ZodValidationMessage<string?>, string?>(
			context,
			editContext,
			() => order.Reference,
			interactive
		);

		// Assert
		zod.MarkupMatches(builtIn.Markup);
		await Assert.That(zod.FindAll("div.validation-message").Count).IsEqualTo(2);
	}

	[Test]
	public async Task ZodValidationMessage_GivenNoMessagesInStaticRender_RendersTheHiddenPlaceholder()
	{
		// Arrange
		Order order = new();
		await using var context = FormHost.CreateContext();
		EditContext editContext = new(order);

		// Act
		var builtIn = RenderMessage<ValidationMessage<string?>, string?>(
			context,
			editContext,
			() => order.Reference,
			false
		);
		var zod = RenderMessage<ZodValidationMessage<string?>, string?>(
			context,
			editContext,
			() => order.Reference,
			false
		);

		// Assert
		zod.MarkupMatches(builtIn.Markup);
		await Assert.That(zod.Find("div").HasAttribute("hidden")).IsTrue();
	}

	[Test]
	public async Task ZodValidationMessage_GivenRichError_AddsCodeAndCategoryAttributes()
	{
		// Arrange
		Order order = new();
		FakeValidator<Order> validator = new()
		{
			Errors = static _ => [At("too_small", "Reference too short", M("Reference"))],
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod());
		await form.ValidateAsync(editContext);

		// Act
		var zod = RenderMessage<ZodValidationMessage<string?>, string?>(
			context,
			editContext,
			() => order.Reference,
			interactive: true,
			showCode: true
		);

		// Assert
		var message = zod.Find("div.validation-message");
		await Assert.That(message.GetAttribute("data-code")).IsEqualTo("too_small");
		await Assert.That(message.GetAttribute("data-category")).IsEqualTo("too_small");
		await Assert.That(message.QuerySelector("span.validation-code")?.TextContent).IsEqualTo("too_small");
		await Assert.That(message.TextContent).StartsWith("Reference too short");
	}

	[Test]
	public async Task ZodValidationMessage_GivenTemplate_RendersItForRichErrors()
	{
		// Arrange
		Order order = new();
		FakeValidator<Order> validator = new()
		{
			Errors = static _ => [At("too_small", "Reference too short", M("Reference"))],
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod());
		await form.ValidateAsync(editContext);
		RenderFragment<ValidationError> template = error =>
			builder =>
			{
				builder.OpenElement(0, "strong");
				builder.AddContent(1, $"{error.Code}: {error.Message}");
				builder.CloseElement();
			};

		// Act
		var zod = context.Render<ZodValidationMessage<string?>>(parameters =>
			parameters
				.Add(static p => p.For, () => order.Reference)
				.Add(static p => p.Template, template)
				.AddCascadingValue(editContext)
		);

		// Assert
		await Assert
			.That(zod.Find("div.validation-message strong").TextContent)
			.IsEqualTo("too_small: Reference too short");
	}

	[Test]
	public async Task ZodValidationMessage_GivenCollectionElementInStaticRender_UsesTheFormFieldName()
	{
		// Arrange
		Order order = new() { Lines = [new(), new()] };
		await using var context = FormHost.CreateContext();
		EditContext editContext = new(order);
		var index = 1;

		// Act
		var builtIn = RenderMessage<ValidationMessage<int>, int>(
			context,
			editContext,
			() => order.Lines[index].Quantity,
			false
		);
		var zod = RenderMessage<ZodValidationMessage<int>, int>(
			context,
			editContext,
			() => order.Lines[index].Quantity,
			false
		);

		// Assert
		await Assert
			.That(zod.Find("div").GetAttribute("data-valmsg-for"))
			.IsEqualTo(builtIn.Find("div").GetAttribute("data-valmsg-for"));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task ZodValidationSummary_GivenPlainMessages_RendersTheSameMarkupAsValidationSummary(bool interactive)
	{
		// Arrange
		Order order = new();
		await using var context = FormHost.CreateContext();
		EditContext editContext = new(order);
		ValidationMessageStore store = new(editContext);
		store.Add(editContext.Field(nameof(Order.Reference)), "Reference is required");
		store.Add(new FieldIdentifier(order, string.Empty), "Order is invalid");

		// Act
		var builtIn = RenderSummary<ValidationSummary>(context, editContext, interactive);
		var zod = RenderSummary<ZodValidationSummary>(context, editContext, interactive);

		// Assert
		zod.MarkupMatches(builtIn.Markup);
		await Assert.That(zod.FindAll("li.validation-message").Count).IsEqualTo(2);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task ZodValidationSummary_GivenNoMessages_RendersTheSameMarkupAsValidationSummary(bool interactive)
	{
		// Arrange
		Order order = new();
		await using var context = FormHost.CreateContext();
		EditContext editContext = new(order);

		// Act
		var builtIn = RenderSummary<ValidationSummary>(context, editContext, interactive);
		var zod = RenderSummary<ZodValidationSummary>(context, editContext, interactive);

		// Assert
		zod.MarkupMatches(builtIn.Markup);
		await Assert.That(zod.FindAll("li").Count).IsEqualTo(0);
	}

	[Test]
	public async Task ZodValidationSummary_GivenModel_ShowsOnlyModelLevelMessagesLikeValidationSummary()
	{
		// Arrange
		Order order = new();
		await using var context = FormHost.CreateContext();
		EditContext editContext = new(order);
		ValidationMessageStore store = new(editContext);
		store.Add(editContext.Field(nameof(Order.Reference)), "Reference is required");
		store.Add(new FieldIdentifier(order, string.Empty), "Order is invalid");

		// Act
		var zod = context.Render<ZodValidationSummary>(parameters =>
			parameters.Add(static p => p.ShowModelOnly, true).AddCascadingValue(editContext)
		);

		// Assert
		await Assert
			.That(zod.FindAll("li").Select(static li => li.TextContent).ToArray())
			.IsEquivalentTo(["Order is invalid"]);
	}

	[Test]
	public async Task ZodValidationSummary_GivenGroupByPath_GroupsRichErrorsUnderTheirPath()
	{
		// Arrange
		Order order = new() { Lines = [new()] };
		FakeValidator<Order> validator = new()
		{
			Errors = static _ =>
				[
					At("too_small", "Quantity too small", M("Lines"), I(0), M("Quantity")),
					At("not_multiple_of", "Quantity must be even", M("Lines"), I(0), M("Quantity")),
					At("too_small", "Reference too short", M("Reference")),
				],
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod());
		await form.ValidateAsync(editContext);

		// Act
		var zod = context.Render<ZodValidationSummary>(parameters =>
			parameters.Add(static p => p.GroupByPath, true).AddCascadingValue(editContext)
		);

		// Assert
		var groups = zod.FindAll("li.validation-group");
		await Assert
			.That(groups.Select(static g => g.GetAttribute("data-path")!).ToArray())
			.IsEquivalentTo(["Lines[0].Quantity", "Reference"]);
		var lines = zod.Find("li.validation-group[data-path='Lines[0].Quantity']")
			.QuerySelectorAll("li.validation-message");
		await Assert
			.That(lines.Select(static l => l.GetAttribute("data-code")!).ToArray())
			.IsEquivalentTo(["too_small", "not_multiple_of"]);
	}

	[Test]
	public async Task ZodValidationSummary_GivenShowUnionDetails_ListsEachOptionsIssues()
	{
		// Arrange
		Order order = new();
		var union = ZodIssues.InvalidUnion([
			[At("invalid_type", "Expected string", M("Reference"))],
			[At("too_small", "Too short", M("Reference"))],
		]);
		FakeValidator<Order> validator = new() { Errors = _ => [union] };
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod());
		await form.ValidateAsync(editContext);

		// Act
		var zod = context.Render<ZodValidationSummary>(parameters =>
			parameters.Add(static p => p.ShowUnionDetails, true).AddCascadingValue(editContext)
		);

		// Assert
		var options = zod.FindAll("ol.validation-union > li.validation-union-option");
		await Assert.That(options.Count).IsEqualTo(2);
		await Assert.That(options[1].QuerySelector("li")?.GetAttribute("data-code")).IsEqualTo("too_small");
	}

	static IRenderedComponent<TComponent> RenderMessage<TComponent, TValue>(
		BunitContext context,
		EditContext editContext,
		Expression<Func<TValue>> accessor,
		bool interactive,
		bool showCode = false
	)
		where TComponent : IComponent
	{
		void Build(ComponentParameterCollectionBuilder<TComponent> parameters)
		{
			parameters.TryAdd("For", accessor);
			parameters.AddCascadingValue(editContext);
			if (showCode)
				parameters.TryAdd("ShowCode", true);

			if (interactive)
				parameters.SetAssignedRenderMode(RenderMode.InteractiveServer);
		}

		return context.Render<TComponent>(Build);
	}

	static IRenderedComponent<TComponent> RenderSummary<TComponent>(
		BunitContext context,
		EditContext editContext,
		bool interactive
	)
		where TComponent : IComponent
	{
		void Build(ComponentParameterCollectionBuilder<TComponent> parameters)
		{
			parameters.AddCascadingValue(editContext);
			if (interactive)
				parameters.SetAssignedRenderMode(RenderMode.InteractiveServer);
		}

		return context.Render<TComponent>(Build);
	}
}
