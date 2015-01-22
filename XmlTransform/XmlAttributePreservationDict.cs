using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;

namespace Microsoft.Web.XmlTransform.Extended
{
    internal class XmlAttributePreservationDict
    {
        #region Private data members
        private readonly List<string> _orderedAttributes = new List<string>();
        private readonly Dictionary<string, string> _leadingSpaces = new Dictionary<string, string>();

        private string _attributeNewLineString;
        private bool _computedOneAttributePerLine;
        private bool _oneAttributePerLine;
        #endregion

        private bool OneAttributePerLine
        {
            get
            {
                if (_computedOneAttributePerLine) return _oneAttributePerLine;
                _computedOneAttributePerLine = true;
                _oneAttributePerLine = ComputeOneAttributePerLine();
                return _oneAttributePerLine;
            }
        }

        internal void ReadPreservationInfo(string elementStartTag)
        {
            Debug.Assert(elementStartTag.StartsWith("<", StringComparison.Ordinal) && elementStartTag.EndsWith(">", StringComparison.Ordinal), "Expected string containing exactly a single tag");
            var whitespaceReader = new WhitespaceTrackingTextReader(new StringReader(elementStartTag));

            var lastCharacter = EnumerateAttributes(elementStartTag,
                (line, linePosition, attributeName) =>
                {
                    _orderedAttributes.Add(attributeName);
                    if (whitespaceReader.ReadToPosition(line, linePosition))
                    {
                        _leadingSpaces.Add(attributeName, whitespaceReader.PrecedingWhitespace);
                    }
                    else
                    {
                        Debug.Fail("Couldn't get leading whitespace for attribute");
                    }
                }
                );

            if (whitespaceReader.ReadToPosition(lastCharacter))
            {
                _leadingSpaces.Add(String.Empty, whitespaceReader.PrecedingWhitespace);
            }
            else
            {
                Debug.Fail("Couldn't get trailing whitespace for tag");
            }
        }

        private int EnumerateAttributes(string elementStartTag, Action<int, int, string> onAttributeSpotted)
        {
            var selfClosed = (elementStartTag.EndsWith("/>", StringComparison.Ordinal));
            var xmlDocString = elementStartTag;
            if (!selfClosed)
            {
                xmlDocString = elementStartTag.Substring(0, elementStartTag.Length - 1) + "/>";
            }

            var xmlReader = new XmlTextReader(new StringReader(xmlDocString)) { Namespaces = false };

            xmlReader.Read();

            var hasMoreAttributes = xmlReader.MoveToFirstAttribute();
            while (hasMoreAttributes)
            {
                onAttributeSpotted(xmlReader.LineNumber, xmlReader.LinePosition, xmlReader.Name);
                hasMoreAttributes = xmlReader.MoveToNextAttribute();
            }

            var lastCharacter = elementStartTag.Length;
            if (selfClosed)
            {
                lastCharacter--;
            }
            return lastCharacter;
        }

        internal void WritePreservedAttributes(XmlAttributePreservingWriter writer, XmlAttributeCollection attributes)
        {
            string oldNewLineString = null;
            if (_attributeNewLineString != null)
            {
                oldNewLineString = writer.SetAttributeNewLineString(_attributeNewLineString);
            }

            try
            {
                foreach (var attributeName in _orderedAttributes)
                {
                    var attr = attributes[attributeName];
                    if (attr == null) continue;
                    if (_leadingSpaces.ContainsKey(attributeName))
                    {
                        writer.WriteAttributeWhitespace(_leadingSpaces[attributeName]);
                    }

                    attr.WriteTo(writer);
                }

                if (_leadingSpaces.ContainsKey(String.Empty))
                {
                    writer.WriteAttributeTrailingWhitespace(_leadingSpaces[String.Empty]);
                }
            }
            finally
            {
                if (oldNewLineString != null)
                {
                    writer.SetAttributeNewLineString(oldNewLineString);
                }
            }
        }

        internal void UpdatePreservationInfo(XmlAttributeCollection updatedAttributes, XmlFormatter formatter)
        {
            if (updatedAttributes.Count == 0)
            {
                if (_orderedAttributes.Count > 0)
                {
                    // All attributes were removed, clear preservation info
                    _leadingSpaces.Clear();
                    _orderedAttributes.Clear();
                }
            }
            else
            {
                var attributeExists = new Dictionary<string, bool>();

                // Prepopulate the list with attributes that existed before
                foreach (var attributeName in _orderedAttributes)
                {
                    attributeExists[attributeName] = false;
                }

                // Update the list with attributes that exist now
                foreach (XmlAttribute attribute in updatedAttributes)
                {
                    if (!attributeExists.ContainsKey(attribute.Name))
                    {
                        _orderedAttributes.Add(attribute.Name);
                    }
                    attributeExists[attribute.Name] = true;
                }

                var firstAttribute = true;
                string keepLeadingWhitespace = null;
                foreach (var key in _orderedAttributes)
                {
                    var exists = attributeExists[key];

                    // Handle removal
                    if (!exists)
                    {
                        // We need to figure out whether to keep the leading
                        // or trailing whitespace. The logic is:
                        //   1. The whitespace before the first attribute is
                        //      always kept, so remove trailing space.
                        //   2. We want to keep newlines, so if the leading
                        //      space contains a newline then remove trailing
                        //      space. If multiple newlines are being removed,
                        //      keep the last one.
                        //   3. Otherwise, remove leading space.
                        //
                        // In order to remove trailing space, we have to 
                        // remove the leading space of the next attribute, so
                        // we store this leading space to replace the next.
                        if (_leadingSpaces.ContainsKey(key))
                        {
                            var leadingSpace = _leadingSpaces[key];
                            if (firstAttribute)
                            {
                                if (keepLeadingWhitespace == null)
                                {
                                    keepLeadingWhitespace = leadingSpace;
                                }
                            }
                            else if (ContainsNewLine(leadingSpace))
                            {
                                keepLeadingWhitespace = leadingSpace;
                            }

                            _leadingSpaces.Remove(key);
                        }
                    }

                    else if (keepLeadingWhitespace != null)
                    {
                        // Exception to rule #2 above: Don't replace an existing
                        // newline with one that was removed
                        if (firstAttribute || !_leadingSpaces.ContainsKey(key) || !ContainsNewLine(_leadingSpaces[key]))
                        {
                            _leadingSpaces[key] = keepLeadingWhitespace;
                        }
                        keepLeadingWhitespace = null;
                    }

                    // Handle addition
                    else if (!_leadingSpaces.ContainsKey(key))
                    {
                        if (firstAttribute)
                        {
                            // This will prevent the textwriter from writing a
                            // newline before the first attribute
                            _leadingSpaces[key] = " ";
                        }
                        else if (OneAttributePerLine)
                        {
                            // Add the indent space between each attribute
                            _leadingSpaces[key] = GetAttributeNewLineString(formatter);
                        }
                        else
                        {
                            // Don't add any hard-coded spaces. All new attributes
                            // should be at the end, so they'll be formatted while
                            // writing. Make sure we have the right indent string,
                            // though.
                            EnsureAttributeNewLineString(formatter);
                        }
                    }

                    // firstAttribute remains true until we find the first
                    // attribute that actually exists
                    firstAttribute = firstAttribute && !exists;
                }
            }
        }

        private bool ComputeOneAttributePerLine()
        {
            if (_leadingSpaces.Count > 1)
            {
                // If there is a newline between each pair of attributes, then
                // we'll continue putting newlines between all attributes. If
                // there's no newline between one pair, then we won't.
                var firstAttribute = true;
                foreach (var attributeName in _orderedAttributes)
                {
                    // The space in front of the first attribute doesn't count,
                    // because that space isn't between attributes.
                    if (firstAttribute)
                    {
                        firstAttribute = false;
                    }
                    else if (_leadingSpaces.ContainsKey(attributeName) &&
                        !ContainsNewLine(_leadingSpaces[attributeName]))
                    {
                        // This means there are two attributes on one line
                        return false;
                    }
                }

                return true;
            }
            // If there aren't at least two original attributes on this
            // tag, then it's not possible to tell if more than one would
            // be on a line. Default to more than one per line.
            // TODO(jodavis): Should we look at sibling tags to decide?
            return false;
        }

        private bool ContainsNewLine(string space)
        {
            return space.IndexOf("\n", StringComparison.Ordinal) >= 0;
        }

        public string GetAttributeNewLineString(XmlFormatter formatter)
        {
            return _attributeNewLineString ?? (_attributeNewLineString = ComputeAttributeNewLineString(formatter));
        }

        private string ComputeAttributeNewLineString(XmlFormatter formatter)
        {
            string lookAheadString = LookAheadForNewLineString();
            if (lookAheadString != null)
            {
                return lookAheadString;
            }
            return formatter != null ? formatter.CurrentAttributeIndent : null;
        }

        private string LookAheadForNewLineString()
        {
            return _leadingSpaces.Values.FirstOrDefault(ContainsNewLine);
        }

        private void EnsureAttributeNewLineString(XmlFormatter formatter)
        {
            GetAttributeNewLineString(formatter);
        }
    }
}
