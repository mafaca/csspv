using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Reflection;

namespace SpirV
{
	public class OperandType
	{
		public virtual bool ReadValue(IReadOnlyList<uint> words, int index, out object value, out int wordsUsed)
		{
			// This returns the dynamic type
			value = GetType();
			wordsUsed = 1;
			return true;
		}
	}

	public class Literal : OperandType
	{
	}

	public class LiteralNumber : Literal
	{
	}

	// The SPIR-V JSON file uses only literal integers
	public class LiteralInteger : LiteralNumber
	{
		public override bool ReadValue(IReadOnlyList<uint> words, int index, out object value, out int wordsUsed)
		{
			value = words[index];
			wordsUsed = 1;
			return true;
		}
	}

	public class LiteralString : Literal
	{
#warning TODO:
		public override bool ReadValue(IReadOnlyList<uint> words, int index, out object value, out int wordsUsed)
		{
			// This is just a fail-safe -- the loop below must terminate
			wordsUsed = 1;

			List<byte> bytes = new List<byte>();
			for (int i = index; i < words.Count; ++i)
			{
				byte[] wordBytes = BitConverter.GetBytes(words[i]);
				int zeroOffset = -1;
				for (int j = 0; j < wordBytes.Length; ++j)
				{
					if (wordBytes[j] == 0)
					{
						zeroOffset = j;
						break;
					}
					else
					{
						bytes.Add(wordBytes[j]);
					}
				}

				if (zeroOffset != -1)
				{
					wordsUsed = i + 1;
					break;
				}
			}

			UTF8Encoding decoder = new UTF8Encoding();
			byte[] byteArray = bytes.ToArray();
			value = Encoding.UTF8.GetString(byteArray, 0, byteArray.Length);
			return true;
		}
	}

	public class LiteralContextDependentNumber : Literal
	{
		// This is handled during parsing by ConvertConstant
	}

	public class LiteralExtInstInteger : Literal
	{
		public override bool ReadValue(IReadOnlyList<uint> words, int index, out object value, out int wordsUsed)
		{
			value = words[index];
			wordsUsed = 1;
			return true;
		}
	}

	public class LiteralSpecConstantOpInteger : Literal
	{
		public override bool ReadValue(IReadOnlyList<uint> words, int index, out object value, out int wordsUsed)
		{
			List<ObjectReference> result = new List<ObjectReference>();
			for (int i = index; i < words.Count; i++)
			{
				ObjectReference objRef = new ObjectReference(words[i]);
				result.Add(objRef);
			}

			value = result;
			wordsUsed = words.Count - index;
			return true;
		}
	}

	public class Parameter
	{
		public virtual IReadOnlyList<OperandType> OperandTypes { get; }
	}

	public class ParameterFactory
	{
		public virtual Parameter CreateParameter(object value)
		{
			return null;
		}
	}

	public class EnumType<T> : EnumType<T, ParameterFactory>
		where T : Enum
	{
	};

#warning TODO:
	public class EnumType<T, U> : OperandType
		where T : Enum
		where U : ParameterFactory, new ()
	{
		public override bool ReadValue(IReadOnlyList<uint> words, int index, out object value, out int wordsUsed)
		{
			int wordsUsedForParameters = 0;
			if (typeof(T).GetTypeInfo().GetCustomAttributes<FlagsAttribute>().Any())
			{
				Dictionary<uint, IReadOnlyList<object>> result = new Dictionary<uint, IReadOnlyList<object>>();
				foreach (var enumValue in EnumerationType.GetEnumValues())
				{
					uint bit = (uint)enumValue;
					// bit == 0 and words [0] == 0 handles the 0x0 = None cases
					if ((words[index] & bit) != 0 || (bit == 0 && words[index] == 0))
					{
						Parameter p = parameterFactory_.CreateParameter(bit);
						List<object> resultItems = new List<object>();
						if (p != null)
						{
							for (int j = 0; j < p.OperandTypes.Count; ++j)
							{
								p.OperandTypes[j].ReadValue(words, 1 + wordsUsedForParameters, out object pValue, out int pWordsUsed);
								wordsUsedForParameters += pWordsUsed;
								resultItems.Add (pValue);
							}
						}
						result[bit] = resultItems;
					}
				}
				value = new BitEnumOperandValue<T>(result);
			}
			else
			{
				List<object> resultItems = new List<object>();
				Parameter p = parameterFactory_.CreateParameter(words[index]);
				if (p != null)
				{
					for (int j = 0; j < p.OperandTypes.Count; ++j)
					{
						p.OperandTypes[j].ReadValue(words, 1 + wordsUsedForParameters, out object pValue, out int pWordsUsed);
						wordsUsedForParameters += pWordsUsed;
						resultItems.Add(pValue);
					}
				}
				value = new ValueEnumOperandValue<T>((T)(object)words[index], resultItems);
			}

			wordsUsed = wordsUsedForParameters + 1;
			return true;
		}

		public System.Type EnumerationType => typeof(T);

		private U parameterFactory_ = new U();
	}

	public class IdScope : OperandType
	{
		public override bool ReadValue(IReadOnlyList<uint> words, int index, out object value, out int wordsUsed)
		{
			value = (Scope)words[index];
			wordsUsed = 1;
			return true;
		}
	}

	public class IdMemorySemantics : OperandType
	{
		public override bool ReadValue(IReadOnlyList<uint> words, int index, out object value, out int wordsUsed)
		{
			value = (MemorySemantics)words[index];
			wordsUsed = 1;
			return true;
		}
	}

	public class IdType : OperandType
	{
		public override bool ReadValue(IReadOnlyList<uint> words, int index, out object value, out int wordsUsed)
		{
			value = words[index];
			wordsUsed = 1;
			return true;
		}
	}

	public class IdResult : IdType
	{
		public override bool ReadValue(IReadOnlyList<uint> words, int index, out object value, out int wordsUsed)
		{
			value = new ObjectReference(words[index]);
			wordsUsed = 1;
			return true;
		}
	}

	public class IdResultType : IdType
	{
	}

	public class IdRef : IdType
	{
		public override bool ReadValue(IReadOnlyList<uint> words, int index, out object value, out int wordsUsed)
		{
			value = new ObjectReference(words[index]);
			wordsUsed = 1;
			return true;
		}
	}

	public class PairIdRefIdRef : OperandType
	{
		public override bool ReadValue(IReadOnlyList<uint> words, int index, out object value, out int wordsUsed)
		{
			ObjectReference variable = new ObjectReference(words[index]);
			ObjectReference parent = new ObjectReference(words[index + 1]);
			value = new { Variable = variable, Parent = parent };
			wordsUsed = 2;
			return true;
		}
	}

	public class PairIdRefLiteralInteger : OperandType
	{
		public override bool ReadValue(IReadOnlyList<uint> words, int index, out object value, out int wordsUsed)
		{
			ObjectReference type = new ObjectReference(words[index]);
			uint word = words[index + 1];
			value = new { Type = type, Member = word };
			wordsUsed = 2;
			return true;
		}
	}

	public class PairLiteralIntegerIdRef : OperandType
	{
		public override bool ReadValue(IReadOnlyList<uint> words, int index, out object value, out int wordsUsed)
		{
			uint selector = words[index];
			ObjectReference label = new ObjectReference(words[index + 1]);
			value = new { Selector = selector, Label = label };
			wordsUsed = 2;
			return true;
		}
	}
}