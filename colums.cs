        public Data GetData(SelectiveAccess access)
        {
            if (access.Index != 1)
                throw new NotImplementedException();

            var entry = (EntryDescriptor)access.Descriptor;

            DateTimeOffset current;              // timeSource + (4 * integrationPeriod)
            int currentIndex = (int)entry.FromEntry;
            var result = new List<Data[]>();

            while (currentIndex <= entry.ToEntry)
            {
                //current = current.Add(integrationPeriod)

                currentIndex++;
            }
            return DataCoder.Encode(result); 
        }
        
        
        
        struct GeneratorColumn {

        public readonly CaptureObjectDefinition Cod;
        public readonly Func<DateTimeOffset, Data> generator;

        public GeneratorColumn(CaptureObjectDefinition cod, Func<DateTimeOffset, Data> generator)
        {
            this.Cod = cod;
            this.generator = generator;
        }
    }
    
    using Elgsis.DP.Protocols.Asn;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Elgsis.DP.Protocols.Dlms.Cosem.IC
{
    class ProfileGenericClass : DlmsClass
    {
        public const int CLASS = 7;
        public const int VER = 1;

        private static readonly Dictionary<byte, DlmsAttributeDesc> attributes = new Dictionary<byte, DlmsAttributeDesc>
        {
            { 1, new DlmsAttributeDesc("logical_name", typeof(ObisCode)) },
            { 2, new DlmsAttributeDesc("buffer", typeof(DataStructure[])) },
            { 3, new DlmsAttributeDesc("capture_objects", typeof(CaptureObjectDefinition[]), true) },
            { 4, new DlmsAttributeDesc("capture_period", typeof(object), true) },
            { 5, new DlmsAttributeDesc("sort_method", typeof(object), true) },
            { 6, new DlmsAttributeDesc("sort_object", typeof(object), true) },
            { 7, new DlmsAttributeDesc("entries_in_use", typeof(UInt32)) },
            { 8, new DlmsAttributeDesc("profile_entries", typeof(UInt32), true) }
        };

        public ProfileGenericClass(ObisCode logicalName, string tag = "")
            : base(CLASS, VER, logicalName, attributes, new Dictionary<byte, MethodAttributeDesc>(), tag)
        {
        }

        public ProfileGenericClass(ObisCode logicalName, Dictionary<byte, DlmsAttributeDesc> attributes, string tag = "")
            : base(CLASS, VER, logicalName, attributes, new Dictionary<byte, MethodAttributeDesc>(), tag)
        {
        }

        public virtual DlmsAttribute Buffer
        {
            get
            {
                return Attributes[2];
            }
        }

        public virtual DlmsAttribute EntriesInUse
        {
            get
            {
                return Attributes[7];
            }
        }

        public virtual DlmsAttribute CaptureObjects
        {
            get
            {
                return Attributes[3];
            }
        }

        public virtual DlmsAttribute ProfileEntries
        {
            get
            {
                return Attributes[8];
            }
        }
    }

    struct RangeDescriptor
    {
        public static readonly byte SelectorId = 1;

        private readonly CaptureObjectDefinition? restrictingObject;
        private readonly object fromValue;
        private readonly object toValue;
        private readonly CaptureObjectDefinition[] selectedValues;

        public RangeDescriptor(CaptureObjectDefinition? restrictingObject, object fromValue, object toValue, CaptureObjectDefinition[] selectedValues)
        {
            if (selectedValues == null)
                throw new ArgumentNullException("selectedValues");

            this.restrictingObject = restrictingObject;
            this.fromValue = fromValue;
            this.toValue = toValue;
            this.selectedValues = selectedValues;
        }

        public CaptureObjectDefinition[] SelectedValues
        {
            get
            {
                return this.selectedValues;
            }
        }

        public object ToValue
        {
            get
            {
                return this.toValue;
            }
        }

        public object FromValue
        {
            get
            {
                return this.fromValue;
            }
        }

        public CaptureObjectDefinition? RestrictingObject
        {
            get
            {
                return this.restrictingObject;
            }
        }
    }

    struct EntryDescriptor
    {
        public static readonly byte SelectorId = 2;

        private readonly UInt32 fromEntry;
        private readonly UInt32 toEntry;
        private readonly UInt16 fromSelectedValue;
        private readonly UInt16 toSelectedValue;

        public EntryDescriptor(UInt32 fromEntry, UInt32 toEntry, UInt16 fromSelectedValue, UInt16 toSelectedValue)
        {
            this.fromEntry = fromEntry;
            this.toEntry = toEntry;
            this.fromSelectedValue = fromSelectedValue;
            this.toSelectedValue = toSelectedValue;
        }

        public ushort ToSelectedValue
        {
            get
            {
                return this.toSelectedValue;
            }
        }

        public ushort FromSelectedValue
        {
            get
            {
                return this.fromSelectedValue;
            }
        }

        public uint ToEntry
        {
            get
            {
                return this.toEntry;
            }
        }

        public uint FromEntry
        {
            get
            {
                return this.fromEntry;
            }
        }

        public override string ToString()
        {
            return string.Format("{0}:{1} {2}:{3}", fromEntry, toEntry, fromSelectedValue, toSelectedValue);
        }
    }

    struct CaptureObjectDefinition : IStringify
    {
        private readonly UInt16 classId;
        private readonly ObisCode logicalName;
        private readonly sbyte attributeIndex;
        private readonly UInt16 dataIndex;

        public CaptureObjectDefinition(UInt16 classId, ObisCode logicalName, sbyte attributeIndex, UInt16 dataIndex)
        {
            this.classId = classId;
            this.logicalName = logicalName;
            this.attributeIndex = attributeIndex;
            this.dataIndex = dataIndex;
        }

        public string Stringify()
        {
            return string.Format("{0}|{1}|{2}|{3}", classId, logicalName.ToString(), attributeIndex, dataIndex);
        }

        public static string PropertyName()
        {
            return "CaptureObjectDefinition";
        }

        public string PropName()
        {
            return PropertyName();
        }

        public static CaptureObjectDefinition Parse(string value)
        {
            var values = value.Split('|');
            if (values.Length != 4)
                throw new ArgumentException("cant parse from this type");

            var ci = UInt16.Parse(values[0]);
            var ln = ObisCode.Parse(values[1]);
            var ai = sbyte.Parse(values[2]);
            var dx = UInt16.Parse(values[3]);
            return new CaptureObjectDefinition(ci, ln, ai, dx);
        }

        public ushort DataIndex
        {
            get
            {
                return this.dataIndex;
            }
        }

        public sbyte AttributeIndex
        {
            get
            {
                return this.attributeIndex;
            }
        }

        public ObisCode LogicalName
        {
            get
            {
                return this.logicalName;
            }
        }

        public ushort ClassId
        {
            get
            {
                return this.classId;
            }
        }

        public static bool operator ==(CaptureObjectDefinition a, CaptureObjectDefinition b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(CaptureObjectDefinition a, CaptureObjectDefinition b)
        {
            return !a.Equals(b);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (!(obj is CaptureObjectDefinition))
                return false;

            var other = (CaptureObjectDefinition)obj;

            return other.ClassId == this.ClassId &&
                   other.DataIndex == this.DataIndex &&
                   other.AttributeIndex == this.AttributeIndex &&
                   other.LogicalName == this.LogicalName;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return this.ClassId.GetHashCode() ^
                        this.DataIndex.GetHashCode() ^
                        this.AttributeIndex.GetHashCode() ^
                        this.LogicalName.GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format("CLS={0} LN={1} ATTR={2} DX={3}",
                classId, logicalName, attributeIndex, dataIndex);
        }
    }
}
