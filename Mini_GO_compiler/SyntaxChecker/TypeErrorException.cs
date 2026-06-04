using System;

namespace Mini_GO_compiler.SyntaxChecker
{
    public class TypeErrorException : Exception
    {
        public TypeErrorException() : base() { }
        public TypeErrorException(string message) : base(message) { }
    }
}
