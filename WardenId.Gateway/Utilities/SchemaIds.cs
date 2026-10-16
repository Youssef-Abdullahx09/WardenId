namespace WardenId.Gateway.Utilities;

/// <summary>
/// OpenAPI schema ids: the type's full name, so same-named records in different features
/// (every feature has a <c>Request</c>) don't collide. Generic types get a readable id
/// (<c>...PaginationListResponseOfClientListItem</c>) instead of the CLR name with assembly
/// qualifiers, which client generators turn into invalid identifiers.
/// </summary>
public static class SchemaIds
{
    public static string For(Type type) =>
        type.IsGenericType ? $"{type.Namespace}.{GenericName(type)}" : type.FullName ?? type.Name;

    private static string GenericName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var name = type.Name[..type.Name.IndexOf('`')];
        return $"{name}Of{string.Join("And", type.GetGenericArguments().Select(GenericName))}";
    }
}
