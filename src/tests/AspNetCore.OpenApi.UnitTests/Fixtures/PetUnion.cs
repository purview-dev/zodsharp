namespace ZodSharp.AspNetCore.OpenApi.Fixtures;

/// <summary>A C# 15 union: the platform describes it with <c>anyOf</c> over the case schemas.</summary>
public union PetUnion(CatCase, DogCase);