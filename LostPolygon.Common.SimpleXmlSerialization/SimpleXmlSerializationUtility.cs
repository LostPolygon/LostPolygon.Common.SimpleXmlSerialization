using System;
using System.IO;
using System.Text;
using System.Xml;

namespace LostPolygon.Common.SimpleXmlSerialization {
    public static class SimpleXmlSerializationUtility {
        public static string GenerateXmlSchemaString<T>(T serializedObject) where T : class {
            if (serializedObject == null)
                return "";

            XmlDocument xmlDocument = new XmlDocument();
            StringBuilder sb = new StringBuilder();
            using (TextWriter textWriter = new StringWriter(sb)) {
                using (XmlTextWriter xmlTextWriter = new XmlTextWriter(textWriter)) {
                    xmlTextWriter.Formatting = Formatting.Indented;
                    xmlTextWriter.IndentChar = ' ';
                    xmlTextWriter.Indentation = 4;

                    const string xmlNamespace = "xs";
                    XmlElement schemaElement = xmlDocument.CreateElement(xmlNamespace, "schema", "http://www.w3.org/2001/XMLSchema");
                    schemaElement.SetAttribute("elementFormDefault", "qualified");
                    schemaElement.SetAttribute("attributeFormDefault", "unqualified");
                    xmlDocument.InsertBefore(schemaElement, null);

                    SchemaGeneratorSimpleXmlSerializer serializer = new SchemaGeneratorSimpleXmlSerializer(xmlDocument, schemaElement);
                    SimpleXmlSerializerBase.InvokeSerializationMethod(serializedObject, serializer);

                    serializer.PostProcess();

                    xmlDocument.WriteContentTo(xmlTextWriter);

                    string unformatted = sb.ToString();
                    sb.Length = 0;
                    xmlDocument.LoadXml(unformatted);
                    xmlDocument.WriteContentTo(xmlTextWriter);
                }
            }

            return sb.ToString();
        }

        public static string XmlSerializeToString<T>(T serializedObject, Encoding encoding = null) where T : class {
            if (serializedObject == null)
                return "";

            XmlDocument xmlDocument = new XmlDocument();
            StringBuilder sb = new StringBuilder();
            using (TextWriter textWriter = new UserEncodingStringWriter(sb, encoding ?? new UTF8Encoding(false))) {
                using (XmlTextWriter xmlTextWriter = new XmlTextWriter(textWriter)) {
                    xmlTextWriter.Formatting = Formatting.Indented;
                    xmlTextWriter.IndentChar = ' ';
                    xmlTextWriter.Indentation = 4;
                    xmlTextWriter.Namespaces = false;

                    SimpleXmlSerializer serializer = new SimpleXmlSerializer(false, xmlDocument, xmlDocument.DocumentElement);
                    SimpleXmlSerializerBase.InvokeSerializationMethod(serializedObject, serializer);

                    xmlDocument.WriteContentTo(xmlTextWriter);
                }
            }

            return sb.ToString();
        }

        public static T XmlDeserializeFromString<T>(string objectData) where T : class {
            XmlDocument xmlDocument = new XmlDocument();

            T result;
            using (TextReader textReader = new StringReader(objectData)) {
                XmlReaderSettings xmlReaderSettings = new XmlReaderSettings {
                    IgnoreWhitespace = true,
                    IgnoreComments = true
                };
                XmlReader xmlReader = XmlReader.Create(textReader, xmlReaderSettings);
                while (xmlReader.NodeType != XmlNodeType.Element) {
                    xmlReader.Read();
                }
                xmlDocument.Load(xmlReader);

                result = SimpleXmlSerializerBase.InvokeSerializationMethod<T>(null, new SimpleXmlSerializer(true, xmlDocument, xmlDocument.DocumentElement));
            }

            return result;
        }

        private class UserEncodingStringWriter : StringWriter {
            private readonly Encoding _encoding;

            public UserEncodingStringWriter(StringBuilder sb, Encoding encoding)
                : base(sb) {
                _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            }

            public override Encoding Encoding => _encoding;
        }
    }
}