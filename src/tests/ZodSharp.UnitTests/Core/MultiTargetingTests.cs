using System.Reflection;
using System.Runtime.Versioning;

namespace ZodSharp.Core;

/// <summary>
/// Verifies the multi-targeting guarantee for the core package: every test run resolves a
/// <see cref="TargetFrameworkAttribute"/> for the core assembly that matches one of the supported target
/// frameworks (<c>net10.0</c>, <c>net11.0</c>).
/// </summary>
public class MultiTargetingTests
{
	static readonly string[] SupportedTargetFrameworkNames = [".NETCoreApp,Version=v10.0", ".NETCoreApp,Version=v11.0"];

	[Test]
	public async Task CoreAssembly_GivenMultiTargetedBuild_DeclaresSupportedTargetFramework()
	{
		// Arrange
		var attribute = typeof(Z).Assembly.GetCustomAttribute<TargetFrameworkAttribute>();

		// Act
		var frameworkName = attribute?.FrameworkName;

		// Assert
		await Assert.That(frameworkName).IsNotNull();
		await Assert.That(SupportedTargetFrameworkNames.Contains(frameworkName!, StringComparer.Ordinal)).IsTrue();
	}
}
