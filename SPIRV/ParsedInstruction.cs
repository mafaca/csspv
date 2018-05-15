﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace SpirV
{
	public class ParsedOperand
	{
		public IList<uint> Words { get; }
		public object Value { get; set; }

		public Operand Operand { get; }

		public ParsedOperand (IList<uint> words, object value, Operand operand)
		{
			Words = words;
			Value = value;
			Operand = operand;
		}

		public T GetSingleEnumValue<T> () where T : System.Enum
		{
			var v = Value as IValueEnumOperandValue;

			if (v.Value.Count == 0) {
				// If there's no value at all, the enum is probably something
				// like ImageFormat. In which case we just return the enum value
				return (T)(object)v.Enumerand;
			} else {
				// This means the enum has a value attached to it, so we return
				// the attached value
				return (T)((IValueEnumOperandValue)Value).Value.First();
			}
		}
	}

	public class VaryingOperandValue
	{
		public VaryingOperandValue (List<object> values)
		{
			Values = values;
		}

		public override string ToString()
		{
			var sb = new StringBuilder ();

			sb.AppendJoin (" ", Values.Select(x => x.ToString ()));

			return sb.ToString ();
		}

		public IReadOnlyList<object> Values {get;}
	}

	public interface IEnumOperandValue
	{
		System.Type EnumerationType { get; }
	}

	public interface IBitEnumOperandValue : IEnumOperandValue
	{
		IReadOnlyDictionary<uint, List<object>> Values { get; }
	}

	public interface IValueEnumOperandValue : IEnumOperandValue
	{
		uint Enumerand { get; }
		List<object> Value { get; }
	}

	public class ValueEnumOperandValue<T> : IValueEnumOperandValue where T : System.Enum
	{
		public System.Type EnumerationType { get { return typeof(T); } }

		public uint Enumerand { get { return (uint)(object)key_; } }
		public List<object> Value { get; }

		private T key_ = default;

		public ValueEnumOperandValue (T key, List<object> value)
		{
			key_ = key;
			Value = value;
		}

		public override string ToString()
		{
			var sb = new StringBuilder();

			sb.Append(key_);
			var valueList = Value as IList<object>;
			if (valueList != null && valueList.Count > 0)
			{
				sb.Append(" ");
				sb.AppendJoin(" ", valueList.Select(x => ParsedInstruction.OperandValueToString(x)));
			}

			return sb.ToString();
		}
	}

	public class BitEnumOperandValue<T> : IEnumOperandValue where T : System.Enum
	{
		public IReadOnlyDictionary<uint, List<object>> Values { get; }
		public System.Type EnumerationType { get { return typeof(T); } }

		public BitEnumOperandValue (Dictionary<uint, List<object>> values)
		{
			Values = values;
		}

		public override string ToString()
		{
			var sb = new StringBuilder ();

			foreach (var key in Values.Keys) {
				sb.Append (EnumerationType.GetEnumName (key));

				var value = Values[key] as IList<object>;

				if (value.Count != 0) {
					sb.Append (" ");
					sb.AppendJoin (" ", value.Select (x => ParsedInstruction.OperandValueToString (x)));
				}
			}

			return sb.ToString ();
		}
	}

	public class ModuleObject
	{

	}

	public class ObjectReference
	{
		public ObjectReference (uint id)
		{
			Id = id;
		}

		public void Resolve (IReadOnlyDictionary<uint, ModuleObject> objects)
		{
			object_ = objects [Id];
		}

		public uint Id { get; }

		private ModuleObject object_;

		public override string ToString ()
		{
			return $"%{Id}";
		}
	}

	public class ParsedInstruction
	{
		public IList<uint> Words { get; }

		public Instruction Instruction { get; }
		public IList<ParsedOperand> Operands { get; } = new List<ParsedOperand> ();

		public ParsedInstruction (int opCode, IList<uint> words)
		{
			Words = words;

			Instruction = Instructions.OpcodeToInstruction [opCode];

			ParseOperands ();
		}

		private void ParseOperands ()
		{
			if (Instruction.Operands.Count == 0) {
				return;
			}

			// Word 0 describes this instruction so we can ignore it
			int currentWord = 1;
			int currentOperand = 0;

			var varyingOperandValues = new List<object> ();
			var varyingWordStart = 0;
			Operand varyingOperand = null;

			for (; currentWord < Words.Count;) {
				var operand = Instruction.Operands [currentOperand];

				operand.Type.ReadValue (Words.Skip (currentWord).ToList (),
					out object value, out int wordsUsed);

				if (operand.Quantifier == OperandQuantifier.Varying) {
					varyingOperandValues.Add (value);
					varyingWordStart = currentWord;
					varyingOperand = operand;
				} else {
					Operands.Add (new ParsedOperand (Words.Skip (currentWord).Take (wordsUsed).ToList (),
						value, operand));
				}

				currentWord += wordsUsed;

				if (operand.Quantifier != OperandQuantifier.Varying) {
					++currentOperand;
				}
			}

			if (varyingOperand != null) {
				Operands.Add (new ParsedOperand (Words.Skip (currentWord).ToList (),
						new VaryingOperandValue (varyingOperandValues), varyingOperand));
			}
		}

		public static string OperandValueToString (object value)
		{
			if (value is System.Type t) {
				return t.Name;
			} else if (value is string s) {
				return $"\"{s}\"";
			} else {
				return value.ToString ();
			}
		}

		private static void AppendValue (StringBuilder sb, object value)
		{
			sb.Append (OperandValueToString (value));
		}

		public Type ResultType { get; private set; }

		public void ResolveResultType (IReadOnlyDictionary<uint, Type> types)
		{
			if (Instruction.Operands.Count > 0 && Instruction.Operands [0].Type is IdResultType) {
				ResultType = types [Words [1]];
			}
		}

		public override string ToString ()
		{
			if (Operands.Count == 0) {
				return Instruction.Name;
			}

			var sb = new StringBuilder ();

			int currentOperand = 0;
			if (Instruction.Operands [currentOperand].Type is IdResultType) {
				sb.Append (ResultType.ToString ());
				sb.Append (" ");

				++currentOperand;
			}

			if (currentOperand < Operands.Count && 
				Instruction.Operands [currentOperand].Type is IdResult) {				
				AppendValue (sb, Operands [currentOperand].Value);
				sb.Append (" = ");

				++currentOperand;
			}

			sb.Append (Instruction.Name);
			sb.Append (" ");

			for (;currentOperand < Operands.Count; ++currentOperand) {
				AppendValue (sb, Operands [currentOperand].Value);
				sb.Append (" ");
			}

			return sb.ToString ();
		}
	}
}
