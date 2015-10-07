using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace eazdevirt.Util
{
	/// <summary>
	/// Calculates the stack state (types on the stack) for each instruction
	/// in a method.
	/// </summary>
	public class StackTypesCalculator
	{
		/// <summary>
		/// Method to walk and calculate stack for.
		/// </summary>
		public MethodDef Method { get; private set; }

		/// <summary>
		/// All calculated stack states mapped by instruction.
		/// </summary>
		private Dictionary<Instruction, Tuple<Stack<TypeSig>, Stack<TypeSig>>> _states;

		/// <summary>
		/// Method instructions.
		/// </summary>
		public IList<Instruction> Instructions
		{
			get
			{
				if (this.Method.HasBody && this.Method.Body.HasInstructions)
					return this.Method.Body.Instructions;
				else
					return new List<Instruction>();
			}
		}

		public StackTypesCalculator(MethodDef method)
		{
			this.Method = method;
			InitStates();
		}

		private void InitStates()
		{
			_states = new Dictionary<Instruction, Tuple<Stack<TypeSig>, Stack<TypeSig>>>();
			foreach (var instr in this.Instructions)
				_states.Add(instr, null);
		}

		/// <summary>
		/// Get the stack state of an instruction in the method, or null if none.
		/// </summary>
		/// <param name="instr">Instruction</param>
		/// <returns>
		/// Stack state when the instruction is executed, or null if none or
		/// instruction not found
		/// </returns>
		public Tuple<Stack<TypeSig>, Stack<TypeSig>> States(Instruction instr)
		{
			Tuple<Stack<TypeSig>, Stack<TypeSig>> state;
			_states.TryGetValue(instr, out state);
			if (state != null)
				return state;
			else
				return null;
		}

		/// <summary>
		/// Check whether or not an instruction maps to a stack state.
		/// </summary>
		/// <param name="instr">Instruction</param>
		/// <returns>true if it maps to a stack state, false if not</returns>
		Boolean HasStackState(Instruction instr)
		{
			return _states[instr] != null;
		}

		/// <summary>
		/// Clone and set a stack state to an instruction.
		/// </summary>
		/// <param name="instr">Instruction</param>
		/// <param name="stack">Stack state to clone</param>
		void SetStackState(Instruction instr, Stack<TypeSig> stackBefore, Stack<TypeSig> stackAfter)
		{
			//Console.WriteLine("Setting state: {0}", instr);
			//WriteStack(stackAfter);
			//Console.WriteLine("-----");

			_states[instr] = new Tuple<Stack<TypeSig>, Stack<TypeSig>>(stackBefore, stackAfter);
		}

		void WriteStack(Stack<TypeSig> stack)
		{
			var clone = CloneStack<TypeSig>(stack);
			Console.WriteLine("Stack[{0}]:", clone.Count);
			while (clone.Count > 0)
				Console.WriteLine(clone.Pop());
		}

		/// <summary>
		/// Perform a shallow clone of a Stack and return the result.
		/// </summary>
		/// <typeparam name="T">Stack type</typeparam>
		/// <param name="stack">Stack to clone</param>
		/// <returns>Cloned stack</returns>
		Stack<T> CloneStack<T>(Stack<T> stack)
		{
			T[] array = new T[stack.Count];
			stack.CopyTo(array, 0);
			return new Stack<T>(array);
		}

		/// <summary>
		/// Get the type loaded onto the stack by a Ldarg instruction.
		/// </summary>
		/// <param name="instr">Instruction</param>
		/// <returns>Loaded type</returns>
		TypeSig LdargType(Instruction instr)
		{
			Int32 operand = 0;
			if (instr.Operand is Parameter)
				operand = (instr.Operand as Parameter).Index;

			var parameters = this.Method.Parameters;
			switch (instr.OpCode.Code)
			{
				case Code.Ldarg:
					return parameters[(UInt16)operand].Type;
				case Code.Ldarg_0:
					return parameters[0].Type;
				case Code.Ldarg_1:
					return parameters[1].Type;
				case Code.Ldarg_2:
					return parameters[2].Type;
				case Code.Ldarg_3:
					return parameters[3].Type;
				case Code.Ldarg_S:
					return parameters[(Byte)operand].Type;
				// Unsure about Ldarga/Ldarga_S
				case Code.Ldarga:
					return new ByRefSig(parameters[(UInt16)operand].Type);
				case Code.Ldarga_S:
					return new ByRefSig(parameters[(Byte)operand].Type);
			}

			return null;
		}

		/// <summary>
		/// Get the type loaded onto the stack by a Ldloc instruction.
		/// </summary>
		/// <param name="instr">Instruction</param>
		/// <returns>Loaded type</returns>
		TypeSig LdlocType(Instruction instr)
		{
			Int32 operand = 0;
			if (instr.Operand is Local)
				operand = (instr.Operand as Local).Index;

			var vars = this.Method.Body.Variables;
			switch (instr.OpCode.Code)
			{
				case Code.Ldloc:
					return vars[(UInt16)operand].Type;
				case Code.Ldloc_0:
					return vars[0].Type;
				case Code.Ldloc_1:
					return vars[1].Type;
				case Code.Ldloc_2:
					return vars[2].Type;
				case Code.Ldloc_3:
					return vars[3].Type;
				case Code.Ldloc_S:
					return vars[(Byte)operand].Type;
				// Unsure about Ldloca/Ldloca_S
				case Code.Ldloca:
					return new ByRefSig(vars[(UInt16)operand].Type);
				case Code.Ldloca_S:
					return new ByRefSig(vars[(Byte)operand].Type);
			}

			return null;
		}

		/// <summary>
		/// Get the type loaded by a math instruction.
		/// </summary>
		/// <param name="instr">Instruction</param>
		/// <param name="state">Current state</param>
		/// <returns>Loaded type</returns>
		TypeSig MathType(Instruction instr, Stack<TypeSig> state)
		{
			switch (instr.OpCode.Code)
			{
				case Code.Add:
				case Code.Add_Ovf:
				case Code.Add_Ovf_Un:
				case Code.Div:
				case Code.Div_Un:
				case Code.Mul:
				case Code.Mul_Ovf:
				case Code.Mul_Ovf_Un:
				case Code.Sub:
				case Code.Sub_Ovf:
				case Code.Sub_Ovf_Un:
				case Code.Shl:
				case Code.Shr:
				case Code.Shr_Un:
					return state.Peek();
				// Unsure about Rem
				case Code.Rem:
				case Code.Rem_Un:
					return state.Peek();
			}

			return null;
		}

		/// <summary>
		/// Gets the types to push onto the stack from an instruction. Should
		/// be run before popping.
		/// </summary>
		/// <param name="instr">Instruction</param>
		/// <param name="state">Current stack state</param>
		/// <returns>Types to push</returns>
		IList<TypeSig> PushTypes(Instruction instr, Stack<TypeSig> state)
		{
			var module = this.Method.Module;
			var list = new List<TypeSig>();
			switch (instr.OpCode.StackBehaviourPush)
			{
				case StackBehaviour.Push0:
				default:
					break;
				case StackBehaviour.Push1:
					TypeSig type;
					if ((type = this.LdargType(instr)) != null
					|| (type = this.LdlocType(instr)) != null
					|| (type = this.MathType(instr, state)) != null)
						list.Add(type);
					else
						throw new Exception(String.Format("Unknown type pushed by Push1 instruction: {0}", instr));
					break;
				case StackBehaviour.Push1_push1:
					// Only Dup has Push1_Push1 behaviour
					if (instr.OpCode.Code == Code.Dup)
					{
						// Assumes value hasn't yet been popped: Will need to make sure that
						// this method is run before popping
						list.Add(state.Peek());
						list.Add(state.Peek());
					}
					break;
				case StackBehaviour.Pushi:
					list.Add(module.CorLibTypes.Int32);
					break;
				case StackBehaviour.Pushi8:
					list.Add(module.CorLibTypes.Int64);
					break;
				case StackBehaviour.Pushr4:
					list.Add(module.CorLibTypes.Single);
					break;
				case StackBehaviour.Pushr8:
					list.Add(module.CorLibTypes.Double);
					break;
				case StackBehaviour.Pushref:
					if (instr.OpCode.Code == Code.Ldstr)
						list.Add(module.CorLibTypes.String);
					else if (instr.OpCode.Code == Code.Ldind_Ref)
						list.Add(state.Peek().Next);
					break;
				case StackBehaviour.Varpush:
					if (instr.Operand is IMethod)
					{
						var method = (instr.Operand as IMethod).ResolveMethodDefThrow();
						if (instr.OpCode.Code == Code.Newobj)
							list.Add(method.DeclaringType.ToTypeSig());
						else
						{
							TypeSig returnType = method.ReturnType;
							if (returnType != null && !returnType.FullName.Equals("System.Void"))
								list.Add(returnType);
						}
					}
					break;
			}

			return list;
		}

		/// <summary>
		/// Get the number of pops an instruction performs.
		/// </summary>
		/// <param name="instr">Instruction</param>
		/// <returns>Pop count</returns>
		Int32 PopCount(Instruction instr)
		{
			switch (instr.OpCode.StackBehaviourPop)
			{
				case StackBehaviour.Pop0:
				default: // PopAll ?
					return 0;
				case StackBehaviour.Pop1:
				case StackBehaviour.Popi:
				case StackBehaviour.Popref:
					return 1;
				case StackBehaviour.Pop1_pop1:
				case StackBehaviour.Popi_pop1:
				case StackBehaviour.Popi_popi:
				case StackBehaviour.Popi_popi8:
				case StackBehaviour.Popi_popr4:
				case StackBehaviour.Popi_popr8:
				case StackBehaviour.Popref_pop1:
				case StackBehaviour.Popref_popi:
					return 2;
				case StackBehaviour.Popi_popi_popi:
				case StackBehaviour.Popref_popi_pop1:
				case StackBehaviour.Popref_popi_popi:
				case StackBehaviour.Popref_popi_popi8:
				case StackBehaviour.Popref_popi_popr4:
				case StackBehaviour.Popref_popi_popr8:
				case StackBehaviour.Popref_popi_popref:
					return 3;
				case StackBehaviour.Varpop:
					if (instr.Operand is IMethod)
					{
						MethodDef method = (instr.Operand as IMethod).ResolveMethodDefThrow();

						if (instr.OpCode.Code == Code.Newobj)
							return method.Parameters.Count - 1;
						else
							return method.Parameters.Count;
					}
					else if (instr.OpCode.Code == Code.Ret)
						return 0; // Unsure?
					throw new Exception("Unexpected operand type for instruction with stack behaviour Varpop");
			}
		}

		void Walk(Instruction start, Stack<TypeSig> state)
		{
			Walk(this.Instructions.IndexOf(start), state);
		}

		void Walk(Int32 offset, Stack<TypeSig> state)
		{
			var instructions = this.Instructions;

			for (Int32 i = offset; i < instructions.Count; i++)
			{
				var instr = instructions[i];
				var before = CloneStack<TypeSig>(state);

				if (HasStackState(instr))
					return;

				// Get types to push
				var pushTypes = PushTypes(instr, state);

				// Pop everything
				for (Int32 p = 0; p < PopCount(instr); p++)
					state.Pop();

				// Push everything
				foreach (var type in pushTypes)
					state.Push(type);

				// Set the stack state
				SetStackState(instr, before, state);

				// Determine where to go next via flow control
				switch (instr.OpCode.FlowControl)
				{
					case FlowControl.Branch:
						Walk(instr.Operand as Instruction, state);
						return;
					case FlowControl.Cond_Branch:
						Walk(instr.Operand as Instruction, state);
						break;
					case FlowControl.Return:
					case FlowControl.Throw:
						return;
					case FlowControl.Call:
					case FlowControl.Next:
					default:
						break;
				}
			}
		}

		public void Walk()
		{
			Walk(0, new Stack<TypeSig>());
		}
	}
}