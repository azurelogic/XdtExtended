using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace Microsoft.Web.XmlTransform.Extended
{
    internal class XmlElementContext : XmlNodeContext
    {
        #region private data members
        private readonly XmlElementContext _parentContext;
        private string _xpath;
        private string _parentXPath;
        private readonly XmlDocument _xmlTargetDoc;

        private readonly IServiceProvider _serviceProvider;

        private XmlNode _transformNodes;
        private XmlNodeList _targetNodes;
        private XmlNodeList _targetParents;

        private XmlAttribute _transformAttribute;
        private XmlAttribute _locatorAttribute;

        private XmlNamespaceManager _namespaceManager;
        #endregion

        public XmlElementContext(XmlElementContext parent, XmlElement element, XmlDocument xmlTargetDoc, IServiceProvider serviceProvider)
            : base(element)
        {
            _parentContext = parent;
            _xmlTargetDoc = xmlTargetDoc;
            _serviceProvider = serviceProvider;
        }

        public T GetService<T>() where T : class
        {
            if (_serviceProvider != null)
            {
                var service = _serviceProvider.GetService(typeof(T)) as T;
                // now it is legal to return service that's null -- due to SetTokenizeAttributeStorage
                //Debug.Assert(service != null, String.Format(CultureInfo.InvariantCulture, "Service provider didn't provide {0}", typeof(ServiceType).Name));
                return service;
            }
            Debug.Fail("No ServiceProvider");
            return null;
        }

        #region data accessors
        public XmlElement Element
        {
            get
            {
                return Node as XmlElement;
            }
        }

        public string XPath
        {
            get { return _xpath ?? (_xpath = ConstructXPath()); }
        }

        public string ParentXPath
        {
            get { return _parentXPath ?? (_parentXPath = ConstructParentXPath()); }
        }

        public Transform ConstructTransform(out string argumentString)
        {
            try
            {
                return CreateObjectFromAttribute<Transform>(out argumentString, out _transformAttribute);
            }
            catch (Exception ex)
            {
                throw WrapException(ex);
            }
        }

        public int TransformLineNumber
        {
            get
            {
                var lineInfo = _transformAttribute as IXmlLineInfo;
                return lineInfo != null ? lineInfo.LineNumber : LineNumber;
            }
        }

        public int TransformLinePosition
        {
            get
            {
                var lineInfo = _transformAttribute as IXmlLineInfo;
                return lineInfo != null ? lineInfo.LinePosition : LinePosition;
            }
        }

        public XmlAttribute TransformAttribute
        {
            get
            {
                return _transformAttribute;
            }
        }

        public XmlAttribute LocatorAttribute
        {
            get
            {
                return _locatorAttribute;
            }
        }
        #endregion

        #region XPath construction
        private string ConstructXPath()
        {
            try
            {
                string argumentString;
                string parentPath = _parentContext == null ? String.Empty : _parentContext.XPath;

                Locator locator = CreateLocator(out argumentString);

                return locator.ConstructPath(parentPath, this, argumentString);
            }
            catch (Exception ex)
            {
                throw WrapException(ex);
            }
        }

        private string ConstructParentXPath()
        {
            try
            {
                string argumentString;
                var parentPath = _parentContext == null ? String.Empty : _parentContext.XPath;

                var locator = CreateLocator(out argumentString);

                return locator.ConstructParentPath(parentPath, this, argumentString);
            }
            catch (Exception ex)
            {
                throw WrapException(ex);
            }
        }

        private Locator CreateLocator(out string argumentString)
        {
            var locator = CreateObjectFromAttribute<Locator>(out argumentString, out _locatorAttribute);
            if (locator != null) return locator;
            argumentString = null;
            //avoid using singleton of "DefaultLocator.Instance", so unit tests can run parallel
            locator = new DefaultLocator();
            return locator;
        }
        #endregion

        #region Context information
        internal XmlNode TransformNode
        {
            get { return _transformNodes ?? (_transformNodes = CreateCloneInTargetDocument(Element)); }
        }

        internal XmlNodeList TargetNodes
        {
            get { return _targetNodes ?? (_targetNodes = GetTargetNodes(XPath)); }
        }

        internal XmlNodeList TargetParents
        {
            get
            {
                if (_targetParents == null && _parentContext != null)
                {
                    _targetParents = GetTargetNodes(ParentXPath);
                }
                return _targetParents;
            }
        }
        #endregion

        #region Node helpers
        private XmlDocument TargetDocument
        {
            get
            {
                return _xmlTargetDoc;
            }
        }

        private XmlNode CreateCloneInTargetDocument(XmlNode sourceNode)
        {
            var infoDocument = TargetDocument as XmlFileInfoDocument;
            XmlNode clonedNode;

            if (infoDocument != null)
            {
                clonedNode = infoDocument.CloneNodeFromOtherDocument(sourceNode);
            }
            else
            {
                XmlReader reader = new XmlTextReader(new StringReader(sourceNode.OuterXml));
                clonedNode = TargetDocument.ReadNode(reader);
            }

            ScrubTransformAttributesAndNamespaces(clonedNode);

            return clonedNode;
        }

        private void ScrubTransformAttributesAndNamespaces(XmlNode node)
        {
            if (node.Attributes != null)
            {
                var attributesToRemove = new List<XmlAttribute>();
                foreach (XmlAttribute attribute in node.Attributes)
                {
                    if (attribute.NamespaceURI == XmlTransformation.TransformNamespace)
                    {
                        attributesToRemove.Add(attribute);
                    }
                    else if (attribute.Prefix != null && (attribute.Prefix.Equals("xmlns") || attribute.Name.Equals("xmlns")))
                    {
                        attributesToRemove.Add(attribute);
                    }
                    else
                    {
                        attribute.Prefix = null;
                    }
                }
                foreach (XmlAttribute attributeToRemove in attributesToRemove)
                {
                    node.Attributes.Remove(attributeToRemove);
                }
            }

            // Do the same recursively for child nodes
            foreach (XmlNode childNode in node.ChildNodes)
            {
                ScrubTransformAttributesAndNamespaces(childNode);
            }
        }

        private XmlNodeList GetTargetNodes(string xpath)
        {
            return TargetDocument.SelectNodes(xpath, GetNamespaceManager());
        }

        private Exception WrapException(Exception ex)
        {
            return XmlNodeException.Wrap(ex, Element);
        }

        private Exception WrapException(Exception ex, XmlNode node)
        {
            return XmlNodeException.Wrap(ex, node);
        }

        private XmlNamespaceManager GetNamespaceManager()
        {
            if (_namespaceManager == null)
            {
                XmlNodeList localNamespaces = Element.SelectNodes("namespace::*");

                if (localNamespaces != null && localNamespaces.Count > 0)
                {
                    if (Element.OwnerDocument != null)
                        _namespaceManager = new XmlNamespaceManager(Element.OwnerDocument.NameTable);

                    foreach (XmlAttribute nsAttribute in localNamespaces)
                    {
                        var index = nsAttribute.Name.IndexOf(':');
                        var prefix = index >= 0 ? nsAttribute.Name.Substring(index + 1) : "_defaultNamespace";

                        if (_namespaceManager != null) _namespaceManager.AddNamespace(prefix, nsAttribute.Value);
                    }
                }
                else
                {
                    _namespaceManager = new XmlNamespaceManager(GetParentNameTable());
                }
            }
            return _namespaceManager;
        }

        private XmlNameTable GetParentNameTable()
        {
            return _parentContext == null ? Element.OwnerDocument.NameTable : _parentContext.GetNamespaceManager().NameTable;
        }

        #endregion

        #region Named object creation
        private static Regex _nameAndArgumentsRegex;
        private Regex NameAndArgumentsRegex
        {
            get { return _nameAndArgumentsRegex ?? (_nameAndArgumentsRegex = new Regex(@"\A\s*(?<name>\w+)(\s*\((?<arguments>.*)\))?\s*\Z", RegexOptions.Compiled | RegexOptions.Singleline)); }
        }

        private string ParseNameAndArguments(string name, out string arguments)
        {
            arguments = null;

            var match = NameAndArgumentsRegex.Match(name);
            if (match.Success)
            {
                if (!match.Groups["arguments"].Success) return match.Groups["name"].Captures[0].Value;
                var argumentCaptures = match.Groups["arguments"].Captures;
                if (argumentCaptures.Count == 1 && !String.IsNullOrEmpty(argumentCaptures[0].Value))
                {
                    arguments = argumentCaptures[0].Value;
                }

                return match.Groups["name"].Captures[0].Value;
            }
            throw new XmlTransformationException(SR.XMLTRANSFORMATION_BadAttributeValue);
        }

        private TObjectType CreateObjectFromAttribute<TObjectType>(out string argumentString, out XmlAttribute objectAttribute) where TObjectType : class
        {
            objectAttribute = Element.Attributes.GetNamedItem(typeof(TObjectType).Name, XmlTransformation.TransformNamespace) as XmlAttribute;
            try
            {
                if (objectAttribute != null)
                {
                    var typeName = ParseNameAndArguments(objectAttribute.Value, out argumentString);
                    if (!String.IsNullOrEmpty(typeName))
                    {
                        var factory = GetService<NamedTypeFactory>();
                        return factory.Construct<TObjectType>(typeName);
                    }
                }
            }
            catch (Exception ex)
            {
                throw WrapException(ex, objectAttribute);
            }

            argumentString = null;
            return null;
        }
        #endregion

        #region Error reporting helpers
        internal bool HasTargetNode(out XmlElementContext failedContext, out bool existedInOriginal)
        {
            failedContext = null;
            existedInOriginal = false;

            if (TargetNodes.Count == 0)
            {
                failedContext = this;
                while (failedContext._parentContext != null &&
                    failedContext._parentContext.TargetNodes.Count == 0)
                {

                    failedContext = failedContext._parentContext;
                }

                existedInOriginal = ExistedInOriginal(failedContext.XPath);
                return false;
            }

            return true;
        }

        internal bool HasTargetParent(out XmlElementContext failedContext, out bool existedInOriginal)
        {
            failedContext = null;
            existedInOriginal = false;

            if (TargetParents.Count == 0)
            {
                failedContext = this;
                while (failedContext._parentContext != null &&
                    !String.IsNullOrEmpty(failedContext._parentContext.ParentXPath) &&
                    failedContext._parentContext.TargetParents.Count == 0)
                {

                    failedContext = failedContext._parentContext;
                }

                existedInOriginal = ExistedInOriginal(failedContext.XPath);
                return false;
            }

            return true;
        }

        private bool ExistedInOriginal(string xpath)
        {
            var service = GetService<IXmlOriginalDocumentService>();
            if (service == null) return false;
            var nodeList = service.SelectNodes(xpath, GetNamespaceManager());
            return nodeList != null && nodeList.Count > 0;
        }
        #endregion
    }
}
