namespace ZodSharp.Core;

/// <summary>
/// A deferred registration of validators into the <see cref="IZodSchemaFactory"/> built by dependency
/// injection. The generated <c>AddZodSharp{Assembly}Schemas()</c> extension registers one per assembly; the
/// factory applies every registration when it is first resolved.
/// </summary>
/// <remarks>
/// This is the trim- and AOT-safe alternative to
/// <see cref="ZodSchemaFactoryExtensions.RegisterFromAssembly(IZodSchemaFactory, System.Reflection.Assembly)"/>:
/// the generated registration names every validator directly, so nothing is discovered through metadata.
/// </remarks>
/// <param name="register">Registers validators into the factory.</param>
public sealed class ZodSchemaRegistration(Action<IZodSchemaFactory> register)
{
	/// <summary>
	/// Gets the callback that registers validators into the factory.
	/// </summary>
	public Action<IZodSchemaFactory> Register { get; } = register ?? throw new ArgumentNullException(nameof(register));
}
