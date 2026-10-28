namespace ZodSharp.AspNetCore.OpenApi.Fixtures;

/// <summary>A C# 15 union: the platform describes it with <c>anyOf</c> over the case schemas.</summary>
public readonly union PetUnion(CatCase, DogCase);
