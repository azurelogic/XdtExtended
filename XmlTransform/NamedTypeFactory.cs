using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Microsoft.Web.XmlTransform.Extended
{
    internal class NamedTypeFactory
    {
        private readonly string _relativePathRoot;
        private readonly List<Registration> _registrations = new List<Registration>();

        internal NamedTypeFactory(string relativePathRoot) {
            _relativePathRoot = relativePathRoot;

            CreateDefaultRegistrations();
        }

        private void CreateDefaultRegistrations() {
            AddAssemblyRegistration(GetType().Assembly, GetType().Namespace);
        }

        internal void AddAssemblyRegistration(Assembly assembly, string nameSpace) {
            _registrations.Add(new Registration(assembly, nameSpace));
        }

        internal void AddAssemblyRegistration(string assemblyName, string nameSpace) {
            _registrations.Add(new AssemblyNameRegistration(assemblyName, nameSpace));
        }

        internal void AddPathRegistration(string path, string nameSpace) {
            if (!Path.IsPathRooted(path)) {
                // Resolve a relative path
                path = Path.Combine(Path.GetDirectoryName(_relativePathRoot), path);
            }

            _registrations.Add(new PathRegistration(path, nameSpace));
        }

        internal TObjectType Construct<TObjectType>(string typeName) where TObjectType : class {
            if (String.IsNullOrEmpty(typeName)) return null;
            var type = GetType(typeName);
            if (type == null) {
                throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture,SR.XMLTRANSFORMATION_UnknownTypeName, typeName, typeof(TObjectType).Name));
            }
            if (!type.IsSubclassOf(typeof(TObjectType))) {
                throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture,SR.XMLTRANSFORMATION_IncorrectBaseType, type.FullName, typeof(TObjectType).Name));
            }
            var constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null) {
                throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture,SR.XMLTRANSFORMATION_NoValidConstructor, type.FullName));
            }
            return constructor.Invoke(new object[] { }) as TObjectType;
        }

        private Type GetType(string typeName) {
            Type foundType = null;
            foreach (var registration in _registrations) {
                if (!registration.IsValid) continue;
                var regType = registration.Assembly.GetType(String.Concat(registration.NameSpace, ".", typeName));
                if (regType == null) continue;
                if (foundType == null) {
                    foundType = regType;
                }
                else {
                    throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture,SR.XMLTRANSFORMATION_AmbiguousTypeMatch, typeName));
                }
            }
            return foundType;
        }

        private class Registration
        {
            private readonly Assembly _assembly;
            private readonly string _nameSpace;

            public Registration(Assembly assembly, string nameSpace) {
                _assembly = assembly;
                _nameSpace = nameSpace;
            }

            public bool IsValid {
                get {
                    return _assembly != null;
                }
            }

            public string NameSpace {
                get {
                    return _nameSpace;
                }
            }

            public Assembly Assembly {
                get {
                    return _assembly;
                }
            }
        }

        private class AssemblyNameRegistration : Registration
        {
            public AssemblyNameRegistration(string assemblyName, string nameSpace)
                : base(Assembly.Load(assemblyName), nameSpace) {
            }
        }

        private class PathRegistration : Registration
        {
            public PathRegistration(string path, string nameSpace)
                : base(Assembly.LoadFile(path), nameSpace) {
            }
        }
    }
}
