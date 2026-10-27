using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGenerator
{
	/// <summary>
	/// Output-scoped context for schema emission. Created inside the source-output callback;
	/// it is not part of the incremental pipeline.
	/// </summary>
	sealed class SchemaGenerationOutputContext(
		GenerationContext<SchemaGenerationCapabilities> context,
		ZodSchemaDescriptor zodSchema,
		AsyncSchemaIndex asyncIndex
	)
	{
		public GenerationContext<SchemaGenerationCapabilities> Context { get; } = context;

		public ZodSchemaDescriptor ZodSchema { get; } = zodSchema;

		public AsyncSchemaIndex AsyncIndex { get; } = asyncIndex;

		public bool RequiresAsync(ZodSchemaDescriptor schema) => AsyncIndex.RequiresAsync(schema.SchemaType);

		/// <summary>Whether a member's nested schema (or any dispatched case schema) needs async validation.</summary>
		public bool NestedRequiresAsync(ZodPropertyDescriptor property) => AsyncIndex.NestedRequiresAsync(property);
	}

	/// <summary>
	/// Decides, at generation time, which schemas need asynchronous validation: those with a custom async
	/// method, an <c>IAsyncValidatableObject</c> target, an <c>AsyncValidationAttribute</c> member, or a nested
	/// schema (transitively) that does. A schema that needs none keeps the synchronous fast path in
	/// <c>ValidateAsync</c>.
	/// </summary>
	/// <remarks>
	/// Built once per output callback from the collected schemas, so it is not pipeline state. A cycle is
	/// resolved by treating a schema that is still being visited as synchronous; any asynchronous member on the
	/// cycle still marks every schema on it.
	/// </remarks>
	sealed class AsyncSchemaIndex
	{
		readonly Dictionary<TypeIdentity, ZodSchemaDescriptor> _schemas = [];
		readonly Dictionary<TypeIdentity, bool> _resolved = [];
		readonly HashSet<TypeIdentity> _visiting = [];

		public AsyncSchemaIndex(IEnumerable<ZodSchemaDescriptor> schemas)
		{
			foreach (var schema in schemas)
				_schemas[schema.SchemaType] = schema;
		}

		public bool RequiresAsync(TypeIdentity schemaType)
		{
			if (_resolved.TryGetValue(schemaType, out var known))
				return known;

			if (!_schemas.TryGetValue(schemaType, out var schema) || !_visiting.Add(schemaType))
				return false;

			var requiresAsync = HasOwnAsyncRules(schema);
			if (!requiresAsync)
			{
				foreach (var property in schema.Properties)
				{
					if (property.ShouldProcess && MemberRequiresAsync(property.Value))
					{
						requiresAsync = true;
						break;
					}
				}
			}

			_visiting.Remove(schemaType);

			// Only a top-level answer is cached: an answer computed while another schema is still being visited
			// may have assumed that schema synchronous to break a cycle.
			if (_visiting.Count == 0 || requiresAsync)
				_resolved[schemaType] = requiresAsync;

			return requiresAsync;
		}

		public bool NestedRequiresAsync(ZodPropertyDescriptor property)
		{
			if (property.NestedSchemaType is { } nested)
			{
				if (_schemas.ContainsKey(nested) ? RequiresAsync(nested) : property.ExternalNestedSchemaRequiresAsync)
					return true;
			}

			foreach (var dispatchCase in property.DispatchCases)
			{
				if (dispatchCase.IsModern && RequiresAsync(dispatchCase.SchemaType))
					return true;
			}

			return false;
		}

		bool MemberRequiresAsync(ZodPropertyDescriptor property) =>
			property.AsyncValidationAttributes.Count > 0 || NestedRequiresAsync(property);

		static bool HasOwnAsyncRules(ZodSchemaDescriptor schema) =>
			schema.CustomValidationMethod.Value.HasCustomValidation || schema.ImplementsAsyncValidatableObject;
	}
}
