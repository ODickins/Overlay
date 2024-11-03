using System.Collections.Concurrent;
using System.Reflection;

namespace Overlay;

public static class Overlay
{
    private static readonly ConcurrentDictionary<(Type TSource, Type TTarget), IList<(PropertyInfo getter, PropertyInfo setter)>> PropertyCache
        = new ConcurrentDictionary<(Type, Type), IList<(PropertyInfo getter, PropertyInfo setter)>>();

    private static readonly ConcurrentDictionary<Type, IList<(PropertyInfo propertyInfo, OverlayAttribute attribute)>> AttributeCache
        = new ConcurrentDictionary<Type, IList<(PropertyInfo propertyInfo, OverlayAttribute attribute)>>();

    public static TTarget Create<TSource, TTarget>(TSource source) where TTarget : new()
        => Copy<TSource, TTarget>(source);

    public static TTarget Copy<TSource, TTarget>(TSource source, TTarget? target = default) where TTarget : new()
    {
        AttributeCache.TryGetValue(typeof(TSource), out var attributes);

        if (attributes is null)
        {
            attributes ??= typeof(TSource)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(x => (x, x.GetCustomAttribute<OverlayAttribute>()))
                .Where(x => x.Item2 is not null)
                .Select(x => (x.x, x.Item2!))
                .ToList();
            AttributeCache.TryAdd(typeof(TSource), attributes);
        }

        var ignoredProperties = attributes
            .Where(x =>
                (x.attribute.CopyOnAdd is false && target is null)
                || (x.attribute.CopyOnModify is false && target is not null)
            )
            .Select(x => x.propertyInfo.Name)
            .ToArray();

        return Copy(source, target, ignoredProperties);
    }

    public static TTarget Create<TSource, TTarget>(TSource source, params string[] ignorePropertyNames) where TTarget : new()
        => Copy<TSource, TTarget>(source, ignorePropertyNames: ignorePropertyNames);

    public static TTarget Copy<TSource, TTarget>(TSource source, TTarget? target = default, params string[] ignorePropertyNames) where TTarget : new()
    {
        target ??= new();
        PropertyCache.TryGetValue((typeof(TSource), typeof(TTarget)), out var propertyInfos);

        if (propertyInfos is null)
        {
            var tSourceInfos = typeof(TSource)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.CanRead)
                .ToArray();

            var tDestinationInfos = typeof(TTarget)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.CanWrite)
                .ToArray();

            propertyInfos ??= new List<(PropertyInfo getter, PropertyInfo setter)>();
            foreach (var getter in tSourceInfos)
            {
                var setter = tDestinationInfos.FirstOrDefault(x =>
                    x.PropertyType == getter.PropertyType
                    && x.Name == getter.Name
                    && x.PropertyType == getter.PropertyType
                );

                if (setter is null)
                    continue;

                propertyInfos.Add((getter, setter));
            }

            PropertyCache.TryAdd((typeof(TSource), typeof(TTarget)), propertyInfos);
        }

        foreach (var info in propertyInfos)
            if (!ignorePropertyNames.Contains(info.setter.Name))
                info.setter.SetValue(target, info.getter.GetValue(source));

        return target;
    }
}