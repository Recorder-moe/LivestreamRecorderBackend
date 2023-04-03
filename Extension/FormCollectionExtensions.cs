using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

namespace LivestreamRecorderBackend.Extension;

public static class FormCollectionExtensions
{
    /// <summary>
    /// Attempts to bind a form collection to a model of type <typeparamref name="T" />.
    /// <see href="https://stackoverflow.com/a/60284398"/>
    /// </summary>
    /// <typeparam name="T">The model type. Must have a public parameterless constructor.</typeparam>
    /// <param name="form">The form data to bind.</param>
    /// <returns>A new instance of type <typeparamref name="T" /> containing the form data.</returns>
    public static T BindToModel<T>(this IFormCollection form) where T : new()
    {
        var props = typeof(T).GetProperties();
        var instance = Activator.CreateInstance<T>();
        var formKeyMap = form.Keys.ToDictionary(k => k.ToUpper(), k => k);

        foreach (var p in props)
        {
            if (p.CanWrite && formKeyMap.ContainsKey(p.Name.ToUpper()))
            {
                // https://stackoverflow.com/a/1089130
                p.SetValue(instance, Convert.ChangeType(form[formKeyMap[p.Name.ToUpper()]].FirstOrDefault(), p.PropertyType));
            }
        }

        return instance;
    }
}
