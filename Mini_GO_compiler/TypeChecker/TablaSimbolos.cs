using Antlr4.Runtime;
using System.Collections.Generic;

namespace Mini_GO_compiler.TypeChecker
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

    // Información extendida para tipos complejos
    public class TypeInfo
    {
        public MiniGoType BaseType { get; set; }
        public MiniGoType ElementType { get; set; } // Para arrays y slices
        public int ArraySize { get; set; } // Para arrays (0 si es slice)
        public Dictionary<string, TypeInfo> StructMembers { get; set; } // Para structs
        public string StructName { get; set; } // Nombre de la estructura

        public TypeInfo(MiniGoType baseType)
        {
            BaseType = baseType;
            ElementType = MiniGoType.Error;
            ArraySize = 0;
            StructMembers = null;
            StructName = null;
        }

        public bool IsArrayOrSlice()
        {
            return BaseType == MiniGoType.IntArray || BaseType == MiniGoType.Slice;
        }

        public bool IsStruct()
        {
            return BaseType == MiniGoType.Struct && StructMembers != null;
        }

        public static TypeInfo CreateSimple(MiniGoType type)
        {
            return new TypeInfo(type);
        }

        public static TypeInfo CreateArray(MiniGoType elementType, int size)
        {
            return new TypeInfo(MiniGoType.IntArray) 
            { 
                ElementType = elementType, 
                ArraySize = size 
            };
        }

        public static TypeInfo CreateSlice(MiniGoType elementType)
        {
            return new TypeInfo(MiniGoType.Slice) 
            { 
                ElementType = elementType 
            };
        }

        public static TypeInfo CreateStruct(string name, Dictionary<string, TypeInfo> members)
        {
            return new TypeInfo(MiniGoType.Struct) 
            { 
                StructName = name,
                StructMembers = members 
            };
        }
    }

    public class TablaSimbolos
    {
        public class Ident
        {
            public IToken Token { get; set; }
            public MiniGoType Type { get; set; } 
            public TypeInfo TypeInfo { get; set; } // Información extendida de tipo
            public ParserRuleContext Decl { get; set; }
        }

        public class VarIdent : Ident
        {
            public bool IsConstant { get; set; }
        }

        public class MethodIdent : Ident
        {
            public LinkedList<TypeInfo> ParamTypes { get; set; }
        }

        private LinkedList<Dictionary<string, Ident>> scopes;
        private Dictionary<string, TypeInfo> userDefinedTypes; // Para structs y types personalizados

        public TablaSimbolos()
        {
            scopes = new LinkedList<Dictionary<string, Ident>>();
            scopes.AddFirst(new Dictionary<string, Ident>()); // Global scope
            userDefinedTypes = new Dictionary<string, TypeInfo>();
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
            var ident = new VarIdent 
            { 
                Token = token, 
                Type = type, 
                TypeInfo = TypeInfo.CreateSimple(type),
                Decl = decl, 
                IsConstant = isConstant 
            };
            scopes.First.Value[token.Text] = ident;
        }

        public void InsertarVariableConTypeInfo(IToken token, TypeInfo typeInfo, ParserRuleContext decl, bool isConstant = false)
        {
            var ident = new VarIdent 
            { 
                Token = token, 
                Type = typeInfo.BaseType, 
                TypeInfo = typeInfo,
                Decl = decl, 
                IsConstant = isConstant 
            };
            scopes.First.Value[token.Text] = ident;
        }

        public void InsertarMetodo(IToken token, MiniGoType returnType, LinkedList<TypeInfo> paramTypes, ParserRuleContext decl)
        {
            var ident = new MethodIdent 
            { 
                Token = token, 
                Type = returnType, 
                TypeInfo = TypeInfo.CreateSimple(returnType),
                ParamTypes = paramTypes, 
                Decl = decl 
            };
            scopes.First.Value[token.Text] = ident;
        }

        public void InsertarTipoUsuario(string name, TypeInfo typeInfo)
        {
            userDefinedTypes[name] = typeInfo;
        }

        public TypeInfo BuscarTipoUsuario(string name)
        {
            if (userDefinedTypes.TryGetValue(name, out TypeInfo typeInfo))
            {
                return typeInfo;
            }
            return null;
        }
    }
}
