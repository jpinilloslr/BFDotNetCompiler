using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace BrainfuckCompiler
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Brainfuck compiler");

            if (args.Length == 1)
            {
                var fileName = args[0];
                var programName = fileName.Replace(" ", "_");

                var parser = new BrainfuckParser();
                var ilGenerator = new IlGenerator(programName);

                try
                {
                    parser.ReadFile(fileName);
                    parser.ParseCode(ilGenerator);
                    IlCompiler.Compile(ilGenerator.GetIlCode(), fileName);
                    Console.WriteLine("Done");
                }
                catch (SyntaxException se)
                {
                    Console.WriteLine("Syntax error: " + se.Message);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error: " + e.Message);
                }

                
            }
            else
            {
                Console.WriteLine("Usage: bfc <sourceFile>");
            }
            
        }
    }

    public class BrainfuckParser
    {
        private string _code;
        private List<Token> _tokens = new List<Token>(); 

        public void ReadFile(string fileName)
        {
            var sr = new StreamReader(new FileStream(fileName + ".txt", FileMode.Open), Encoding.ASCII);
            _code = sr.ReadToEnd();
            sr.Close();
        }

        private void Tokenize()
        {
            _tokens = new List<Token>();

            for (var i = 0; i < _code.Length; i++)
            {
                var count = 1;
                while (i < _code.Length-1 && _code[i + 1] == _code[i])
                {
                    i++;
                    count++;
                }
                var token = Token.Get(_code[i], count);
                if (token != null)
                    _tokens.Add(token);
            }
        }

        private void CheckSyntaxErrors()
        {
            var branches = 0;
            foreach (var c in _code)
            {
                switch (c)
                {
                    case '[':
                        branches++;
                        break;
                    case ']':
                        branches--;
                        break;
                }
            }
            if (branches != 0)
            {
                throw new SyntaxException("There are some missing square brackets.");
            }
        }

        public void ParseCode(IlGenerator ilGenerator)
        {
            CheckSyntaxErrors();
            Tokenize();
            foreach (var token in _tokens)
            {
                ParseToken(ilGenerator, token);
            }
        }

        private static void ParseToken(IlGenerator ilGenerator, Token token)
        {
            switch (token.Op)
            {
                case Token.OpCode.IncPtr:
                    ilGenerator.IncrementPointer(token.Count);
                    return;
                case Token.OpCode.DecPtr:
                    ilGenerator.DecrementPointer(token.Count);
                    return;
                case Token.OpCode.IncPtd:
                    ilGenerator.IncrementPointedValue(token.Count);
                    return;
                case Token.OpCode.DecPtd:
                    ilGenerator.DecrementPointedValue(token.Count);
                    return;
                case Token.OpCode.Write:
                    for (var i = 0; i < token.Count; i++)
                        ilGenerator.Write();                   
                    return;
                case Token.OpCode.Read:
                    for (var i = 0; i < token.Count; i++)
                        ilGenerator.Read();
                    return;
                case Token.OpCode.BranchStart:
                    for (var i = 0; i < token.Count; i++)
                        ilGenerator.OpenBranch();
                    return;
                case Token.OpCode.BranchEnd:
                    for (var i = 0; i < token.Count; i++)
                        ilGenerator.CloseBranch();
                    return;
            }
        }
    }
    
    public class Token
    {
        public enum OpCode
        {
            IncPtr,
            DecPtr,
            IncPtd,
            DecPtd,
            Write,
            Read,
            BranchStart,
            BranchEnd
        };

        private static readonly IDictionary<char, OpCode> OpCodesDictionary = new Dictionary<char, OpCode>
        {
            ['>'] = OpCode.IncPtr,
            ['<'] = OpCode.DecPtr,
            ['+'] = OpCode.IncPtd,
            ['-'] = OpCode.DecPtd,
            [','] = OpCode.Read,
            ['.'] = OpCode.Write,
            ['['] = OpCode.BranchStart,
            [']'] = OpCode.BranchEnd,
        };

        public static Token Get(char op, int count = 1)
        {
            OpCode opCode;
            return (OpCodesDictionary.TryGetValue(op, out opCode))
                ? new Token { Op = opCode, Count = count}
                : null;
        }

        public OpCode Op { get; set; }
        public int Count { get; set; }
    }

    public class IlGenerator
    {
        private string _code = "";
        private readonly string _programName;
        private readonly Stack<string> _branchesStack = new Stack<string>();
        private int _branchCounter;

        public IlGenerator(string programName)
        {
            _programName = programName;
        }

        public void OpenBranch()
        {
            var branchName = "Branch" + (_branchCounter++);
            _branchesStack.Push(branchName);

            _code += $"BranchStart{branchName}: nop\r\n"+
                     $"ldsfld     uint8[] {_programName}.Program::Memory\r\n" +
                     $"ldsfld     int32 {_programName}.Program::Counter\r\n" +
                     "ldelem.u1\r\n" +
                     "stloc.0\r\n" +
                     "ldloc.0\r\n" +
                     $"brfalse BranchEnd{branchName}\r\n\r\n";
        }

        public void CloseBranch()
        {
            var branchName = _branchesStack.Pop();
            _code += $"br BranchStart{branchName}\r\n" +
                     $"BranchEnd{branchName}: nop\r\n\r\n";
        }

        public void IncrementPointedValue(int count)
        {
            _code += $"ldsfld     uint8[] {_programName}.Program::Memory\r\n" +
                     $"ldsfld     int32 {_programName}.Program::Counter\r\n" +
                     "ldelema    [mscorlib]System.Byte\r\n" +
                     "dup\r\n" +
                     "ldind.u1\r\n" +
                     $"ldc.i4 {count}\r\n" +
                     "add\r\n" +
                     "conv.u1\r\n" +
                     "stind.i1\r\n\r\n";
        }

        public void DecrementPointedValue(int count)
        {
            _code += $"ldsfld     uint8[] {_programName}.Program::Memory\r\n" +
                     $"ldsfld     int32 {_programName}.Program::Counter\r\n" +
                     "ldelema    [mscorlib]System.Byte\r\n" +
                     "dup\r\n" +
                     "ldind.u1\r\n" +
                     $"ldc.i4 {count}\r\n" +
                     "sub\r\n" +
                     "conv.u1\r\n" +
                     "stind.i1\r\n\r\n";
        }

        public void IncrementPointer(int count)
        {
            _code += $"ldsfld     int32 {_programName}.Program::Counter\r\n" +
                     $"ldc.i4 {count}\r\n" +
                     "add\r\n" +
                     $"stsfld int32 {_programName}.Program::Counter\r\n\r\n";
        }

        public void DecrementPointer(int count)
        {
            _code += $"ldsfld     int32 {_programName}.Program::Counter\r\n" +
                     $"ldc.i4 {count}\r\n" +
                     "sub\r\n" +
                     $"stsfld int32 {_programName}.Program::Counter\r\n\r\n";
        }

        public void Write()
        {
            _code += 
                     $"ldsfld     uint8[] {_programName}.Program::Memory\r\n" +
                     $"ldsfld     int32 {_programName}.Program::Counter\r\n" +
                     "ldelem.u1\r\n" +
                     "stloc.0\r\n" +
                     "ldloc.0\r\n" +
                     "call       void [mscorlib]System.Console::Write(char)\r\n\r\n";
        }

        public void Read()
        {
            _code += "call       int32 [mscorlib]System.Console::Read()\r\n" +
                     "stloc.0\r\n" +
                     $"ldsfld     uint8[] {_programName}.Program::Memory\r\n" +
                     $"ldsfld     int32 {_programName}.Program::Counter\r\n" +
                     "ldloc.0\r\n" +
                     "conv.u1\r\n" +
                     "stelem.i1\r\n\r\n";
        }

        public string GetIlCode()
        {
            return
                ".assembly extern mscorlib\r\n" +
                "{\r\n" +
                "  auto\r\n" +
                "}\r\n" +
                $".assembly {_programName} {{}}\r\n" +
                $".module {_programName}.exe\r\n" +
                "\r\n" +
                $".class private auto ansi beforefieldinit {_programName}.Program\r\n" +
                "       extends [mscorlib]System.Object\r\n" +
                "{\r\n" +
                  ".field private static initonly uint8[] Memory\r\n" +
                "  .field private static int32 Counter\r\n" +
                  "" +
                "  .method public hidebysig specialname rtspecialname \r\n" +
                "          instance void  .ctor() cil managed\r\n" +
                "  {\r\n" +
                    ".maxstack  8\r\n" +
                "    ldarg.0\r\n" +
                "    call       instance void [mscorlib]System.Object::.ctor()\r\n" +
                "    nop\r\n" +
                "    ret\r\n" +
                "  }\r\n" +
                "\r\n" +
                "  .method private hidebysig specialname rtspecialname static \r\n" +
                "          void  .cctor() cil managed\r\n" +
                "  {\r\n" +
                    ".maxstack  8\r\n" +
                "    ldc.i4     0x26e8f0\r\n" +
                "    newarr     [mscorlib]System.Byte\r\n" +
                $"    stsfld     uint8[] {_programName}.Program::Memory\r\n" +
                "    ret\r\n" +
                "  } \r\n" +
                  "\r\n" +
                "  .method private hidebysig static void  Main(string[] args) cil managed\r\n" +
                "  {\r\n" +
                    ".entrypoint\r\n" +
                "    .maxstack  8\r\n" +
                "    .locals init ([0] char c)\r\n" +
                    "\r\n" +
                    _code +
                "    ret\r\n" +
                "  } \r\n" +
                "}\r\n";        
        }
    }

    public static class IlCompiler
    {
        private static void SaveTempFile(string code, string fileName)
        {
            using (var sw = new StreamWriter(new FileStream(fileName + ".il", FileMode.Create), Encoding.ASCII))
            {
                sw.Write(code);
                sw.Close();
            }
        }

        private static void DeleteTempFile(string fileName)
        {
            File.Delete(fileName + ".il");
        }

        private static void CompileTempFile(string fileName)
        {
            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = false,
                UseShellExecute = false,
                FileName = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\ilasm.exe",
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = fileName
            };

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    process?.WaitForExit();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
        }

        public static void Compile(string code, string appName)
        {
            SaveTempFile(code, appName);            
            CompileTempFile(appName);
            DeleteTempFile(appName);
        }
    }

    public class SyntaxException : Exception
    {
        public SyntaxException(string message)
            :base(message)
        {            
        }
    }
}