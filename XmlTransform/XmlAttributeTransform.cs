using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;

namespace Microsoft.Web.XmlTransform.Extended
{
    public abstract class AttributeTransform : Transform
    {
        #region private data members
        private XmlNode _transformAttributeSource;
        private XmlNodeList _transformAttributes;
        private XmlNode _targetAttributeSource;
        private XmlNodeList _targetAttributes;
        #endregion

        protected AttributeTransform()
            : base(TransformFlags.ApplyTransformToAllTargetNodes)
        {
        }

        protected XmlNodeList TransformAttributes
        {
            get
            {
                if (_transformAttributes == null || _transformAttributeSource != TransformNode)
                {
                    _transformAttributeSource = TransformNode;
                    _transformAttributes = GetAttributesFrom(TransformNode);
                }
                return _transformAttributes;
            }
        }

        protected XmlNodeList TargetAttributes
        {
            get
            {
                if (_targetAttributes == null || _targetAttributeSource != TargetNode)
                {
                    _targetAttributeSource = TargetNode;
                    _targetAttributes = GetAttributesFrom(TargetNode);
                }
                return _targetAttributes;
            }
        }

        private XmlNodeList GetAttributesFrom(XmlNode node)
        {
            if (Arguments == null || Arguments.Count == 0)
            {
                return GetAttributesFrom(node, "*", false);
            }
            if (Arguments.Count == 1)
            {
                return GetAttributesFrom(node, Arguments[0], true);
            }
            // First verify all the arguments
            foreach (var argument in Arguments)
            {
                GetAttributesFrom(node, argument, true);
            }

            // Now return the complete XPath and return the combined list
            return GetAttributesFrom(node, Arguments, false);
        }

        private XmlNodeList GetAttributesFrom(XmlNode node, string argument, bool warnIfEmpty)
        {
            return GetAttributesFrom(node, new[] { argument }, warnIfEmpty);
        }

        private XmlNodeList GetAttributesFrom(XmlNode node, IList<string> arguments, bool warnIfEmpty)
        {
            var array = new string[arguments.Count];
            arguments.CopyTo(array, 0);
            var xpath = String.Concat("@", String.Join("|@", array));

            var attributes = node.SelectNodes(xpath);
            if (attributes == null || (attributes.Count != 0 || !warnIfEmpty)) return attributes;
            Debug.Assert(arguments.Count == 1, "Should only call warnIfEmpty==true with one argument");
            if (arguments.Count == 1)
            {
                Log.LogWarning(SR.XMLTRANSFORMATION_TransformArgumentFoundNoAttributes, arguments[0]);
            }

            return attributes;
        }
    }
}
