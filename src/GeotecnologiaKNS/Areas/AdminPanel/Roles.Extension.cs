namespace GeotecnologiaKNS.Infra;

using System.Reflection;

public static partial class Roles
{
    #region Static methods
    public static IEnumerable<RoleClaims> GetRoleClaims()
    {
        return typeof(Roles)
        .GetProperties()
        .Select(PropertyValue);
    }

    private static RoleClaims PropertyValue(PropertyInfo property) =>
        (RoleClaims)property.GetValue(null)!;
    #endregion
}