using System;
using System.Collections.Generic;
using System.Reflection;
using dnlib.DotNet.Emit;
using eazdevirt.Reflection;

namespace eazdevirt.Detection.V1
{
	/// <summary>
	/// Instruction detector for assemblies protected with "V1" virtualization
	/// (v3.4 - v4.9).
	/// </summary>
	public sealed class InstructionDetectorV1 : InstructionDetectorBase
	{
		/// <summary>
		/// Singleton instance.
		/// </summary>
		public static InstructionDetectorV1 Instance { get { return _instance; } }
		private static InstructionDetectorV1 _instance = new InstructionDetectorV1();

		/// <summary>
		/// Delegate for detector methods.
		/// </summary>
		/// <param name="instruction">Virtual instruction</param>
		/// <returns>true if detected, false if not</returns>
		private delegate Boolean Detector(EazVirtualInstruction instruction);

		/// <summary>
		/// Dictionary mapping CIL opcodes to their respective detector methods.
		/// </summary>
		private Dictionary<Code, Detector> _detectors = new Dictionary<Code, Detector>();

		/// <summary>
		/// Construct an InstructionDetectorV1.
		/// </summary>
		private InstructionDetectorV1()
		{
			this.Initialize();
		}

		/// <summary>
		/// Build the detector dictionary via reflection.
		/// </summary>
		private void Initialize()
		{
			var extensions = typeof(eazdevirt.Detection.V1.Ext.Extensions);
			var methods = extensions.GetMethods();
			foreach (var method in methods)
			{
				var attrs = method.GetCustomAttributes<DetectAttribute>();
				foreach (var attr in attrs)
				{
					Detector detector = (Detector)Delegate.CreateDelegate(typeof(Detector), method);
					if (attr != null && detector != null)
					{
						this.AddDetector(attr, detector);
					}
				}
			}
		}

		/// <summary>
		/// Add a detector to the dictionary, printing a warning if one already exists for
		/// a CIL opcode.
		/// </summary>
		/// <param name="attr">Attribute</param>
		/// <param name="callback">Detector delegate</param>
		private void AddDetector(DetectAttribute attr, Detector callback)
		{
			if (!_detectors.ContainsKey(attr.OpCode))
				_detectors.Add(attr.OpCode, callback);
			else
			{
				Console.WriteLine("[WARNING] More than one detector method found for opcode {0}", attr.OpCode);
				_detectors[attr.OpCode] = callback;
			}
		}

		/// <inheritdoc/>
		public override Code Identify(EazVirtualInstruction instruction)
		{
			foreach (var kvp in _detectors)
			{
				if (kvp.Value(instruction))
					return kvp.Key;
			}

			throw new OriginalOpcodeUnknownException(instruction);
		}

		/// <inheritdoc/>
		public override DetectAttribute IdentifyFull(EazVirtualInstruction instruction)
		{
			foreach (var kvp in _detectors)
			{
				if (kvp.Value(instruction))
					return (DetectAttribute)kvp.Value.Method.GetCustomAttribute(typeof(DetectAttribute));
			}

			throw new OriginalOpcodeUnknownException(instruction);
		}
	}
}
