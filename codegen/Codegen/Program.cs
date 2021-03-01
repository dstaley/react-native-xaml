﻿using MiddleweightReflection;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using System;

namespace Codegen
{
    public enum ViewManagerPropertyType
    {
        Unknown = -1,
        Boolean = 0,
        Number = 1,
        String = 2,
        Array = 3,
        Map = 4,
        Color = 5,
    };
    public partial class TypeCreator
    {
        public TypeCreator(IEnumerable<MrType> types)
        {
            Types = types;
        }

        public IEnumerable<MrType> Types { get; set; }
    }

    public partial class TypeProperties
    {
        public TypeProperties(IEnumerable<MrProperty> properties)
        {
            Properties = properties;
        }
        IEnumerable<MrProperty> Properties { get; set; }
    }

    public partial class TypeEvents
    {
        public TypeEvents(IEnumerable<MrEvent> events)
        {
            Events = events;
        }
        IEnumerable<MrEvent> Events { get; set; }
    }

    public partial class TSProps
    {
        public TSProps(IEnumerable<MrType> types) { Types = types; }
        IEnumerable<MrType> Types { get; set; }
    }

    public partial class TSTypes
    {
        public TSTypes(IEnumerable<MrType> types) { Types = types; }
        IEnumerable<MrType> Types { get; set; }
    }

    public class NameEqualityComparer : IEqualityComparer<MrTypeAndMemberBase> {
        public bool Equals(MrTypeAndMemberBase that, MrTypeAndMemberBase other)
        {
            return that.GetName() == other.GetName();
        }

        public int GetHashCode([DisallowNull] MrTypeAndMemberBase obj)
        {
            return obj.GetName().GetHashCode();
        }
    }


class Program
    {
        const string Windows_winmd = @"C:\Program Files (x86)\Windows Kits\10\UnionMetadata\10.0.19041.0\Windows.winmd";
        private void DumpTypes()
        {
            var context = new MrLoadContext(true);
            context.FakeTypeRequired += (sender, e) =>
            {
                var ctx = sender as MrLoadContext;
                if (e.AssemblyName == "Windows.Foundation.FoundationContract" || e.AssemblyName == "Windows.Foundation.UniversalApiContract")
                {
                    e.ReplacementType = ctx.GetTypeFromAssembly(e.TypeName, "Windows");
                }
            };
            var windows_winmd = context.LoadAssemblyFromPath(Windows_winmd);
            var winmd = winmdPath != null ? context.LoadAssemblyFromPath(winmdPath) : windows_winmd;
            context.FinishLoading();
            var types = winmd.GetAllTypes().Skip(1);
            Util.LoadContext = context;

            var baseClassesToProject = new string[]
            {
                "Windows.UI.Xaml.UIElement",
                "Windows.UI.Xaml.Controls.Primitives.FlyoutBase",
            };

            var xamlTypes = types.Where(type => baseClassesToProject.Any(b =>
                Util.DerivesFrom(type, b)) || type.GetName() == "DependencyObject");
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var generatedDirPath = Path.GetFullPath(cppOutPath ?? Path.Join(assemblyLocation, @"..\..\..\..", @"..\package\windows\ReactNativeXaml\Codegen"));
            var packageSrcPath = Path.GetFullPath(tsOutPath ?? Path.Join(assemblyLocation, @"..\..\..\..", @"..\package\src"));

            var creatableTypes = xamlTypes.Where(x => Util.HasCtor(x)).ToList();
            creatableTypes.Sort((a, b) => a.GetName().CompareTo(b.GetName()));
            var typeCreatorGen = new TypeCreator(creatableTypes).TransformText();

            if (!Directory.Exists(generatedDirPath))
            {
                Directory.CreateDirectory(generatedDirPath);
            }
            File.WriteAllText(Path.Join(generatedDirPath, "TypeCreator.g.cpp"), typeCreatorGen);

            var properties = new List<MrProperty>();
            var events = new List<MrEvent>();
            foreach (var type in xamlTypes)
            {
                var props = type.GetProperties();
                var propsToAdd = props.Where(p => Util.ShouldEmitPropertyMetadata(p));
                properties.AddRange(propsToAdd);

                var eventsToAdd = type.GetEvents().Where(e => Util.ShouldEmitEventMetadata(e));
                events.AddRange(eventsToAdd);
            }

            var propsGen = new TSProps(xamlTypes).TransformText();
            File.WriteAllText(Path.Join(packageSrcPath, "Props.ts"), propsGen);

            var typesGen = new TSTypes(xamlTypes).TransformText();
            File.WriteAllText(Path.Join(packageSrcPath, "Types.tsx"), typesGen);

            properties.Sort((a, b) => a.GetName().CompareTo(b.GetName()));
            var propertiesGen = new TypeProperties(properties).TransformText();
            File.WriteAllText(Path.Join(generatedDirPath, "TypeProperties.g.h"), propertiesGen);

            var enumConvertersGen = new EnumConverters().TransformText();
            File.WriteAllText(Path.Join(generatedDirPath, "EnumConverters.g.cpp"), enumConvertersGen);

            var eventsGen = new TypeEvents(events).TransformText();
            File.WriteAllText(Path.Join(generatedDirPath, "TypeEvents.g.h"), eventsGen);
        }

        private string winmdPath { get; set; }
        private string cppOutPath { get; set; }
        private string tsOutPath { get; set; }
        class OptionDef
        {
            public int NumberOfParams { get; set; }
            public Action<Program, string> Action { get; set; }
        }

        static void PrintHelp()
        {
            foreach (var k in optionDefs.Keys)
            {
                Console.WriteLine(k);
            }
        }

        static Dictionary<string, OptionDef> optionDefs = new Dictionary<string, OptionDef>() {
                { "-help", new OptionDef (){ NumberOfParams = 1, Action = (_, _2) => { PrintHelp(); } } },
                { "-winmd", new OptionDef (){ NumberOfParams = 2, Action = (p, v) => { p.winmdPath = v; } } },
                { "-cppout", new OptionDef (){ NumberOfParams = 2, Action = (p, v) => { p.cppOutPath = v; } } },
                { "-tsout", new OptionDef (){ NumberOfParams = 2, Action = (p, v) => { p.tsOutPath = v; } } },
            };

        static void Main(string[] args)
        {
            var p = new Program();
            for (int i = 0; i < args.Length;)
            {
                if (optionDefs.ContainsKey(args[i]))
                {
                    var def = optionDefs[args[i]];
                    string v = null;
                    if (def.NumberOfParams == 2 && i < args.Length - 1) {
                        v = args[i + 1];
                    }
                    i += def.NumberOfParams;
                    def.Action(p, v);
                } else
                {
                    throw new ArgumentException($"Unkown option {args[i]}");
                }
            }
            p.DumpTypes();
        }
    }
}

