namespace WardenId.Gateway.Utilities;

/// <summary>
/// OpenAPI schema ids: the type's full name, so same-named records in different features
/// (every feature has a <c>Request</c>, every list query an <c>Item</c>) don't collide. Generic types get a readable id
/// (<c>...PaginationListResponseOf...Users.List.Item</c>) instead of the CLR name with assembly
/// qualifiers, which client generators turn into invalid identifiers; their type arguments are full names too.
/// </summary>
public static class SchemaIds
{
    public static string For(Type type) =>
        type.IsGenericType ? $"{type.Namespace}.{GenericName(type)}" : NonGenericName(type);

    private static string GenericName(Type type)
    {
        var name = type.Name[..type.Name.IndexOf('`')];
        return $"{name}Of{string.Join("And", type.GetGenericArguments().Select(For))}";
    }

    // Nested types' full names separate the declaring type with '+'.
    private static string NonGenericName(Type type) => (type.FullName ?? type.Name).Replace('+', '.');
}
