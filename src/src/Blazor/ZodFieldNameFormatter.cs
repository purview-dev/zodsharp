using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace ZodSharp.Blazor;

/// <summary>
/// Renders a field accessor the way Blazor names statically rendered inputs (<c>Model.Lines[0].Quantity</c>), for
/// the <c>data-valmsg-for</c> attribute that ties a message to its input in static SSR.
/// </summary>
static class ZodFieldNameFormatter
{
	public static string Format(LambdaExpression accessor)
	{
		List<string> parts = [];
		var current = accessor.Body;
		while (current is not null)
		{
			switch (current)
			{
				case UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary:
					current = unary.Operand;
					continue;

				case MemberExpression { Expression: ConstantExpression or null } captured:
					// A captured variable or a member of the component (Model): the root of the name.
					parts.Add(captured.Member.Name);
					current = null;
					continue;

				case MemberExpression member:
					parts.Add("." + member.Member.Name);
					current = member.Expression;
					continue;

				case MethodCallExpression { Method.Name: "get_Item", Arguments.Count: 1, Object: not null } indexer:
					parts.Add("[" + FormatIndex(indexer.Arguments[0]) + "]");
					current = indexer.Object;
					continue;

				case BinaryExpression { NodeType: ExpressionType.ArrayIndex } arrayIndex:
					parts.Add("[" + FormatIndex(arrayIndex.Right) + "]");
					current = arrayIndex.Left;
					continue;

				default:
					current = null;
					continue;
			}
		}

		parts.Reverse();
		StringBuilder builder = new();
		foreach (var part in parts)
			builder.Append(part);

		var name = builder.ToString();
		return name.StartsWith('.') ? name[1..] : name;
	}

	static string FormatIndex(Expression index)
	{
		var value = index switch
		{
			ConstantExpression constant => constant.Value,
			MemberExpression { Expression: ConstantExpression closure, Member: FieldInfo field } => field.GetValue(
				closure.Value
			),
			MemberExpression { Expression: ConstantExpression closure, Member: PropertyInfo property } =>
				property.GetValue(closure.Value),
			_ => null,
		};

		return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
	}
}
