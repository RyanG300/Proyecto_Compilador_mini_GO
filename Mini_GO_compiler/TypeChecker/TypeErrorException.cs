using System;

namespace Mini_GO_compiler.TypeChecker
{
    public class TypeErrorException : Exception
    {
        public TypeErrorException() : base() { }
        public TypeErrorException(string message) : base(message) { }
    }
}
