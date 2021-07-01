using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Parse.Abstractions.Platform.Objects;
using System.Reflection;
using Parse.Infrastructure;
using Parse.Infrastructure.Data;

namespace Parse
{
    public static class IParseObjectExtensions
    {

        public static async Task<ParseObject> GetByPropertyAsync(this IParseObject parseObject, ParseClient parseClient, string propertyName)
        {
            var (key, value) = parseObject.GetKeyAndValue(propertyName);
            var query = parseClient.GetQuery(parseObject.GetClassName()).WhereEqualTo(key, value);
            return await query.FirstOrDefaultAsync();
        }

        public static PropertyInfo GetKeyProperty(this IParseObject parseObject)
            => parseObject.GetType()
                .GetProperties()
                .Select(p => new { Property = p, ParseKey = p.GetCustomAttributes().SingleOrDefault(c => c is ParseKeyAttribute) })
                .SingleOrDefault(c => c.ParseKey != null)?.Property;

        public static Tuple<string, object> GetKeyAndValue(this IParseObject parseObject, string propertyName)
        {
            var objectType = parseObject.GetType();
            var properties = objectType.GetProperties();
            var property = properties.Single(p => p.Name.Equals(propertyName));
            var parseFieldNameAttribute = property.GetCustomAttributes().SingleOrDefault(c => c is ParseFieldNameAttribute);

            var value = property.GetValue(parseObject);

            if (property.PropertyType.IsEnum)
                value = (int) value;

            if (parseFieldNameAttribute == null)
                return new Tuple<string, object>(Char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1), value);

            return new Tuple<string, object>(((ParseFieldNameAttribute) parseFieldNameAttribute).FieldName, value);
        }

        public static string GetClassName(this IParseObject parseObject)
        {
            var objectType = parseObject.GetType();
            var parseClassAttribute = (ParseClassNameAttribute) parseObject.GetType().GetCustomAttributes(typeof(ParseClassNameAttribute)).FirstOrDefault();

            if (parseClassAttribute == null)
                return objectType.Name;

            return parseClassAttribute.ClassName;
        }

        public static ParseObject CreateEmptyParseObject(this IParseObject parseObject, ParseClient parseClient)
        {
            var product = new ParseObject(parseObject.GetClassName());
            product.Bind(parseClient);
            return product;
        }

        public static async Task<ParseObject> SaveAsync(this IParseObject parseObject)
            => await SaveAsync(parseObject, parseObject.GetParseClient(), null, null);

        private static async Task<ParseObject> SaveAsync(this IParseObject customObject, ParseClient parseClient, IParseObject parentCustomObject, ParseObject parentParseObject)
        {
            var keyProperty = customObject.GetKeyProperty();

            if (keyProperty == null)
                throw new NullReferenceException($"You must define one property with the ParseKeyAttribute.");

            var parseCustomObject = await customObject.GetByPropertyAsync(parseClient, keyProperty.Name);

            if (parseCustomObject == null)
                parseCustomObject = customObject.CreateEmptyParseObject(parseClient);

            foreach (var property in customObject.GetType().GetProperties().Where(p => p.CanRead))
            {
                var (key, value) = customObject.GetKeyAndValue(property.Name);

                if (property.PropertyType.IsEnum)
                {
                    parseCustomObject.Set(key, (int) value);
                    continue;
                }

                if (value is IParseObject innerParseObject)
                {
                    var reference = parentParseObject;

                    if (value != parentCustomObject)
                        reference = await innerParseObject.SaveAsync(parseClient, customObject, parseCustomObject);

                    parseCustomObject.Set(key, reference);
                    continue;
                }

                if (property.PropertyType.IsGenericType &&
                        property.PropertyType.GetGenericTypeDefinition() == typeof(List<>) &&
                        property.PropertyType.GenericTypeArguments.SelectMany(t => t.GetInterfaces()).Any(i => i == typeof(IParseObject)))
                {
                    var relation = parseCustomObject.GetRelation<ParseObject>(key);

                    foreach (var childParseObject in (IEnumerable<IParseObject>) value)
                    {
                        relation.Add(await childParseObject.SaveAsync(parseClient, customObject, parseCustomObject));
                    }

                    continue;
                }

                if (!ParseDataEncoder.Validate(value))
                    continue;

                parseCustomObject.Set(key, value);
            }

            await parseCustomObject.SaveAsync();
            return parseCustomObject;
        }
    }
}
