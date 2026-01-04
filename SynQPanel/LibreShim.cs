// LibreShim.cs
// Minimal compatibility shim for LibreHardwareMonitor types used across the app.
// PURPOSE: compile-time placeholders only. Do NOT add heavy logic here.

using System;
using System.Collections.Generic;

namespace LibreHardwareMonitor.Hardware
{
    // Common enums (expanded with missing values)
    public enum SensorType
    {
        Voltage,
        Current,
        Clock,
        Temperature,
        Load,
        Fan,
        Flow,
        Control,
        Level,
        Factor,
        Power,
        Data,
        Frequency,
        Energy,
        Noise,
        Conductivity,
        Humidity,
        Throughput,
        SmallData,   // <-- added per request
        Unknown
    }

    public enum HardwareType
    {
        Other,
        Unknown,
        Motherboard,
        SuperIO,
        Cpu,
        Memory,
        GpuNvidia,
        GpuAmd,
        GpuIntel,           // added
        Storage,
        Network,
        Psu,                // added
        Cooler,             // added
        Battery,            // added
        EmbeddedController, // added
        // add others only when the compiler requests them
    }

    // Simple identifier placeholder: many callers call Identifier.ToString()
    public class Identifier
    {
        private readonly string _id;
        public Identifier(string id) { _id = id ?? string.Empty; }
        public override string ToString() => _id;
    }

    // Minimal visitor interface expected by UpdateVisitor/Computer.Accept
    public interface IVisitor
    {
        void VisitComputer(IComputer computer);
        void VisitHardware(IHardware hardware);
        void VisitSensor(ISensor sensor);
        void VisitParameter(IParameter parameter);
    }

    // Parameter stub
    public interface IParameter
    {
        string Name { get; }
        object Value { get; }
    }

    // Minimal IComputer interface + class
    public interface IComputer
    {
        IList<IHardware> Hardware { get; }
        void Open();
        void Close();
        void Accept(IVisitor visitor);

        // Libre's real Computer has Traverse(IVisitor) used by UpdateVisitor.VisitComputer.
        // Add it so code calling computer.Traverse(visitor) compiles.
        void Traverse(IVisitor visitor);
    }

    // Minimal Computer implementation (no real hardware access)
    public class Computer : IComputer
    {
        public Computer()
        {
            Hardware = new List<IHardware>();
        }

        public IList<IHardware> Hardware { get; }

        // runtime flags - present in original API — harmless here
        public bool IsCpuEnabled { get; set; }
        public bool IsGpuEnabled { get; set; }
        public bool IsMemoryEnabled { get; set; }
        public bool IsMotherboardEnabled { get; set; }
        public bool IsControllerEnabled { get; set; }
        public bool IsNetworkEnabled { get; set; }
        public bool IsStorageEnabled { get; set; }

        public void Open()
        {
            // no-op shim; real Libre will populate Hardware
        }

        public void Close()
        {
            // no-op shim
        }

        public void Accept(IVisitor visitor)
        {
            try
            {
                visitor?.VisitComputer(this);
                foreach (var hw in Hardware)
                {
                    hw.Accept(visitor);
                }
            }
            catch { /* defensive: shim should not throw */ }
        }

        // Implement Traverse to match real API usage (visitor.Traverse in UpdateVisitor)
        public void Traverse(IVisitor visitor)
        {
            try
            {
                // The real Traverse typically causes a depth-first visit of hardware tree.
                foreach (var hw in Hardware)
                {
                    // call visitor.VisitHardware then let hardware accept
                    try { visitor?.VisitHardware(hw); } catch { }
                    try { hw.Accept(visitor); } catch { }
                }
            }
            catch { /* defensive */ }
        }
    }

    // Minimal IHardware interface your code calls (Update, SubHardware, Sensors, Accept, Identifier, Name, HardwareType)
    public interface IHardware
    {
        string Name { get; }
        Identifier Identifier { get; }
        HardwareType HardwareType { get; }
        IList<IHardware> SubHardware { get; }
        IList<ISensor> Sensors { get; }

        void Update();
        void Accept(IVisitor visitor);
    }

    // Minimal ISensor interface used by your code.
    public interface ISensor
    {
        string Name { get; }
        Identifier Identifier { get; }
        IHardware Hardware { get; }   // back-reference to owning hardware
        SensorType SensorType { get; }
        int Index { get; }            // ordering index (if available)
        double? Value { get; }
        double? Min { get; }
        double? Max { get; }
    }

    // Small helper base classes you may choose to use in tests if you want to create stub hardware/sensors
    // (optional) - not required for compile, but useful for unit tests
    public abstract class HardwareBase : IHardware
    {
        public virtual string Name { get; protected set; } = "";
        public virtual Identifier Identifier { get; protected set; } = new Identifier("");
        public virtual HardwareType HardwareType { get; protected set; } = HardwareType.Unknown;
        public IList<IHardware> SubHardware { get; } = new List<IHardware>();
        public IList<ISensor> Sensors { get; } = new List<ISensor>();

        public virtual void Update() { /* no-op in shim */ }
        public virtual void Accept(IVisitor visitor)
        {
            visitor?.VisitHardware(this);
            foreach (var hw in SubHardware) hw.Accept(visitor);
            foreach (var s in Sensors) visitor?.VisitSensor(s);
        }
    }

    public abstract class SensorBase : ISensor
    {
        public virtual string Name { get; protected set; } = "";
        public virtual Identifier Identifier { get; protected set; } = new Identifier("");
        public virtual IHardware Hardware { get; protected set; }
        public virtual SensorType SensorType { get; protected set; } = SensorType.Unknown;
        public virtual int Index { get; protected set; } = 0;
        public virtual double? Value { get; protected set; } = null;
        public virtual double? Min { get; protected set; } = null;
        public virtual double? Max { get; protected set; } = null;
    }
}
