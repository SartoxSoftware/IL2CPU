﻿using System;
using System.Linq;
using System.Text;

namespace Cosmos.IL2CPU.CIL.Utils.Extensions
{
    public static class TypeExtensions
    {
        public static string GetFullName(this Type aType)
        {
            if (aType.IsGenericParameter)
            {
                return aType.Name;
            }
            var xSB = new StringBuilder();
            if (aType.IsArray)
            {
                xSB.Append(aType.GetElementType().GetFullName());
                xSB.Append("[");
                int xRank = aType.GetArrayRank();
                while (xRank > 1)
                {
                    xSB.Append(",");
                    xRank--;
                }
                xSB.Append("]");
                return xSB.ToString();
            }
            if (aType.IsByRef && aType.HasElementType)
            {
                return "&" + aType.GetElementType().GetFullName();
            }
            if (aType.IsGenericType)
            {
                xSB.Append(aType.GetGenericTypeDefinition().FullName);
            }
            else
            {
                xSB.Append(aType.FullName);
            }
            if (aType.ContainsGenericParameters)
            {
                xSB.Append("<");
                var xArgs = aType.GetGenericArguments();
                for (int i = 0; i < xArgs.Length - 1; i++)
                {
                    xSB.Append(GetFullName(xArgs[i]));
                    xSB.Append(", ");
                }
                if (xArgs.Length == 0)
                {
                    Console.Write("");
                }
                xSB.Append(GetFullName(xArgs.Last()));
                xSB.Append(">");
            }
            return xSB.ToString();
        }
    }
}
