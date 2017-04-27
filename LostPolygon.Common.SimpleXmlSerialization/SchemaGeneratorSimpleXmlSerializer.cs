using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;

namespace LostPolygon.Common.SimpleXmlSerialization {
    public class SchemaGeneratorSimpleXmlSerializer : SimpleXmlSerializer {
        private const string kXmlSchemaNamespace = "http://www.w3.org/2001/XMLSchema";
        private readonly Dictionary<string, XmlElement> _typeElements;
        private SimpleXmlSerializerFlags _flags;

        public string XsdNamespace { get; }
        protected bool IsOptional => (_flags & SimpleXmlSerializerFlags.IsOptional) != 0;

        public SchemaGeneratorSimpleXmlSerializer(
            XmlDocument xmlDocument,
            XmlElement currentXmlElement,
            string xsdNamespace = "xs")
            : base(false, xmlDocument, currentXmlElement) {
            _typeElements = new Dictionary<string, XmlElement>();
            XsdNamespace = !String.IsNullOrEmpty(xsdNamespace) ? xsdNamespace : throw new ArgumentException(nameof(xsdNamespace));
        }

        protected SchemaGeneratorSimpleXmlSerializer(
            Dictionary<string, XmlElement> typeElements,
            XmlDocument xmlDocument,
            XmlElement currentXmlElement,
            string xsdNamespace)
            : base(false, xmlDocument, currentXmlElement) {
            _typeElements = typeElements;
            XsdNamespace = !String.IsNullOrEmpty(xsdNamespace) ? xsdNamespace : throw new ArgumentException(nameof(xsdNamespace));
        }

        protected override SimpleXmlSerializerBase Clone() {
            return new SchemaGeneratorSimpleXmlSerializer(_typeElements, Document, CurrentXmlElement, XsdNamespace);
        }

        public override bool ProcessAttributeString(string name, Action<string> readAction, Func<string> writeFunc) {
            ProcessAttributeString(name, $"{XsdNamespace}:string", writeFunc);

            return true;
        }

        public override bool ProcessStartElement(string name, string prefix = null, string namespaceUri = null) {
           base.ProcessStartElement("element", XsdNamespace, kXmlSchemaNamespace);
           base.ProcessAttributeString("name", null, () => name);
           if (IsOptional) {
               base.ProcessAttributeString("minOccurs", null, () => "0");
           }
           base.ProcessStartElement("complexType", XsdNamespace, kXmlSchemaNamespace);

           return true;
        }

        public override void ProcessEndElement() {
            base.ProcessEndElement();
            base.ProcessEndElement();
        }

        public override void ProcessEnterChildOnRead() {
        }

        public override void ProcessCollection<T>(
            ICollection<T> collection,
            Func<SimpleXmlSerializerBase, T> createItemFunc = null) {

            base.ProcessStartElement("choice", XsdNamespace, kXmlSchemaNamespace);
            if (IsOptional) {
                base.ProcessAttributeString("minOccurs", null, () => "0");
            }
            base.ProcessAttributeString("maxOccurs", null, () => "unbounded");
            {
                // Select one instance of each element
                var grouping = collection
                    .GroupBy(arg => arg.GetType())
                    .Select(grp => grp.First());

                foreach (T value in grouping) {
                    XmlElement capturedTypeElement =
                        CaptureType(value.GetType().Name, () => {
                            CloneSerializerAndInvokeSerializationMethod(value);
                        });
                    if (capturedTypeElement == null)
                        throw new InvalidOperationException();

                    base.ProcessStartElement("element", XsdNamespace, kXmlSchemaNamespace);
                    {
                        base.ProcessAttributeString("name", null, () => capturedTypeElement.GetAttribute("name"));
                        base.ProcessAttributeString("type", null, () => value.GetType().Name);
                    }
                    base.ProcessEndElement();
                }
            }
            base.ProcessEndElement();
        }

        public override void ProcessCollectionAsReadOnly<T>(
            Action<ReadOnlyCollection<T>> collectionSetAction,
            Func<ReadOnlyCollection<T>> collectionGetFunc,
            Func<SimpleXmlSerializerBase, T> createItemFunc = null) {
            ProcessCollection(collectionGetFunc(), createItemFunc);
        }

        public override bool ProcessEnumAttribute<T>(string name, Action<T> readAction, Func<T> writeFunc) {
            Type enumType = typeof(T);
            if (!enumType.IsEnum)
                throw new SerializationException("value must be an Enum");

            CaptureType(enumType.Name, () => {
                base.ProcessStartElement("attribute", XsdNamespace, kXmlSchemaNamespace);
                base.ProcessAttributeString("name", null, () => name);
                if (!IsOptional) {
                    base.ProcessAttributeString("use", null, () => "required");
                }
                {
                    base.ProcessStartElement("simpleType", XsdNamespace, kXmlSchemaNamespace);
                    {
                        base.ProcessStartElement("restriction", XsdNamespace, kXmlSchemaNamespace);
                        base.ProcessAttributeString("base", null, () => $"{XsdNamespace}:string");
                        {
                            string[] enumNames = Enum.GetNames(enumType);
                            for (int i = 0; i < enumNames.Length; i++) {
                                string enumName = enumNames[i];
                                base.ProcessStartElement("enumeration", XsdNamespace, kXmlSchemaNamespace);
                                {
                                    base.ProcessAttributeString("value", null, () => enumName);
                                }
                                base.ProcessEndElement();
                            }
                        }
                        base.ProcessEndElement();
                    }
                    base.ProcessEndElement();
                }
                base.ProcessEndElement();
            });

            ProcessAttributeString(name, enumType.Name, () => Enum.GetName(enumType, writeFunc()));

            return true;
        }

        public override void ProcessFlagsEnumAttributes<T>(T defaultValue, Action<T> readAction, Func<T> writeFunc) {
            Type enumType = typeof(T);
            if (!enumType.IsEnum)
                throw new SerializationException("value must be an Enum");

            T[] enumValues = (T[]) Enum.GetValues(enumType);
            string[] enumNames = Enum.GetNames(enumType);

            long currentEnumValue = writeFunc().ToInt64(null);
            for (int i = 0; i < enumNames.Length; i++) {
                string flagName = enumNames[i];
                long flagValue = enumValues[i].ToInt64(null);

                long currentFlag = currentEnumValue & flagValue;
                ProcessAttributeString(flagName, $"{XsdNamespace}:string", () => Convert.ToString(currentFlag != 0));
            }
        }

        public override void ProcessUnorderedSequence(Action action) {
            if ((_flags & SimpleXmlSerializerFlags.CollectionUnorderedRequired) != 0) {
                base.ProcessStartElement("all", XsdNamespace, kXmlSchemaNamespace);
                {
                    action();
                }
                base.ProcessEndElement();
            } else if ((_flags & SimpleXmlSerializerFlags.CollectionOrdered) != 0) {
                base.ProcessStartElement("sequence", XsdNamespace, kXmlSchemaNamespace);
                {
                    action();
                }
                base.ProcessEndElement();
            } else {
                action();
            }
        }

        public override void ProcessWithFlags(SimpleXmlSerializerFlags flags, Action action) {
            SimpleXmlSerializerFlags prevFlags = _flags;
            _flags |= flags;
            action();
            _flags = prevFlags;
        }

        public void PostProcess() {
            foreach (KeyValuePair<string, XmlElement> pair in _typeElements) {
                XmlElement typeDeclarationElement = pair.Value;
                foreach (XmlAttribute attribute in typeDeclarationElement.Attributes) {
                    typeDeclarationElement.FirstChild.Attributes.Append(attribute);
                }
                typeDeclarationElement = (XmlElement) typeDeclarationElement.FirstChild;
                typeDeclarationElement.SetAttribute("name", pair.Key);

                XmlNode[] sortedChildNodes =
                    typeDeclarationElement
                    .ChildNodes
                    .Cast<XmlNode>()
                    .OrderBy(node => node.LocalName == "attribute")
                    .ToArray();

                foreach (XmlNode childNode in typeDeclarationElement.ChildNodes.Cast<XmlNode>().ToArray())
                {
                    typeDeclarationElement.RemoveChild(childNode);
                }

                foreach (XmlNode node in sortedChildNodes)
                {
                    typeDeclarationElement.AppendChild(node);
                }

                Document.DocumentElement.InsertBefore(typeDeclarationElement, null);
                Document.DocumentElement.InsertBefore(Document.CreateWhitespace(Environment.NewLine+Environment.NewLine), typeDeclarationElement);
            }
        }

        private XmlElement CaptureType(string typeName, Action action) {
            if (_typeElements.ContainsKey(typeName))
                return _typeElements[typeName];

            XmlElement currentXmlElement = CurrentXmlElement;

            XmlElement typeDeclarationElement = Document.CreateElement("typeDeclTemp");
            CurrentXmlElement = typeDeclarationElement;

            action();
            typeDeclarationElement = (XmlElement) typeDeclarationElement.FirstChild;
            _typeElements.Add(typeName, typeDeclarationElement);

            CurrentXmlElement = currentXmlElement;
            return typeDeclarationElement;
        }

        private void ProcessAttributeString(string name, string typeName, Func<string> writeFunc) {
            base.ProcessStartElement("attribute", XsdNamespace, kXmlSchemaNamespace);
            {
                base.ProcessAttributeString("name", null, () => name);
                base.ProcessAttributeString("type", null, () => typeName);
                if (!IsOptional)
                {
                    base.ProcessAttributeString("use", null, () =>  "required");
                }
            }
            base.ProcessEndElement();
        }
    }
}
