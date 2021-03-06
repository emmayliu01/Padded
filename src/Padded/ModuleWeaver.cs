﻿using System;
using System.Linq;
using Mono.Cecil;

public class ModuleWeaver
{
    public const string PaddingFieldPrefix = "$padding_";
    private const int CacheLineSize = 64;
    private const int MinimalSizeOfObject = 4;

    public Action<string> LogDebug { get; set; }

    public Action<string> LogInfo { get; set; }

    public Action<string> LogWarning { get; set; }

    public Action<string> LogError { get; set; }

    public IAssemblyResolver AssemblyResolver { get; set; }

    public ModuleDefinition ModuleDefinition { get; set; }

    public ModuleWeaver()
    {
        LogDebug = m => { };
        LogInfo = m => { };
        LogWarning = m => { };
        LogError = m => { };
    }

    public void Execute()
    {
        foreach (var paddedType in ModuleDefinition.Types.Where(HasPaddedAttribute))
        {
            if (paddedType.IsInterface)
            {
                throw new Exception("Padded.Fody cannot add padding to the interface");
            }

            if (paddedType.IsAbstract)
            {
                throw new Exception("Padded.Fody should be used only on concrete classes");
            }

            PadType(paddedType);
        }
    }

    public static void PadType(TypeDefinition t)
    {
        // using TypeAttributes.SequentialLayout is useless as any reference type makes the type auto
        // the approach is to:
        // 1) add 16 objects, they will kept at the beginning
        // 2) add 64 single bytes to the end to allow CLR move them till the end
        // 3) add one 64bytes long struct to allow CLR move it to the beginning

        var i = 0;

        var @byte = t.Module.ImportReference(typeof(byte));
        var @object = t.Module.ImportReference(typeof(object));

        for (var j = 0; j < CacheLineSize / MinimalSizeOfObject; j++)
        {
            t.Fields.Insert(0, GetField(@object, ref i));
        }
        for (var j = 0; j < CacheLineSize + 1; j++)
        {
            t.Fields.Add(GetField(@byte, ref i));
        }
    }

    private static FieldDefinition GetField(TypeReference type, ref int i)
    {
        i += 1;
        return new FieldDefinition(GetName(i), FieldAttributes.Private, type);
    }

    private static string GetName(int i)
    {
        return $"{PaddingFieldPrefix}{i}";
    }

    private static bool HasPaddedAttribute(TypeDefinition type)
    {
        var attrs = type.CustomAttributes;
        return attrs.Any() && attrs.Any(ca => ca.AttributeType.FullName == "Padded.Fody.PaddedAttribute");
    }
}