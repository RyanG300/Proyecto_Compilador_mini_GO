using Antlr4.Runtime;
using System.Collections.Generic;

namespace Mini_GO_compiler.SyntaxChecker
{
    public enum MiniGoType
    {
        Int = 0,
        Float = 1,
        String = 2,
        Rune = 3,
        Bool = 4,
        IntArray = 5,
        Slice = 6,
        Struct = 7,
        Void = 8,
        Error = -1
    }

    public class TablaSimbolos
    {
        public class Ident
        {
            public IToken Token { get; set; }
            public MiniGoType Type { get; set; } 
            public ParserRuleContext Decl { get; set; }
        }

        public class VarIdent : Ident
        {
            public bool IsConstant { get; set; }
        }

        public class MethodIdent : Ident
        {
            public LinkedList<MiniGoType> Params { get; set; }
        }

        private LinkedList<Dictionary<string, Ident>> scopes;

        public TablaSimbolos()
        {
            scopes = new LinkedList<Dictionary<string, Ident>>();
            scopes.AddFirst(new Dictionary<string, Ident>()); // Global scope
        }

        public void OpenScope()
        {
            scopes.AddFirst(new Dictionary<string, Ident>());
        }

        public void CloseScope()
        {
            if (scopes.Count > 1)
                scopes.RemoveFirst();
        }

        public Ident BuscarNivelActual(string name)
        {
            if (scopes.First.Value.TryGetValue(name, out Ident ident))
            {
                return ident;
            }
            return null;
        }

        public Ident Buscar(string name)
        {
            foreach (var scope in scopes)
            {
                if (scope.TryGetValue(name, out Ident ident))
                {
                    return ident;
                }
            }
            return null;
        }

        public void InsertarVariable(IToken token, MiniGoType type, ParserRuleContext decl, bool isConstant = false)
        {
            var ident = new VarIdent { Token = token, Type = type, Decl = decl, IsConstant = isConstant };
            scopes.First.Value[token.Text] = ident;
        }

        public void InsertarMetodo(IToken token, MiniGoType returnType, LinkedList<MiniGoType> parameters, ParserRuleContext decl)
        {
            var ident = new MethodIdent { Token = token, Type = returnType, Params = parameters, Decl = decl };
            scopes.First.Value[token.Text] = ident;
        }
    }
}
