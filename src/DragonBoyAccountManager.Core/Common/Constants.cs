namespace DragonBoyAccountManager.Core.Common;

public static class Constants
{
    public const string NullTerminator = "\0";
    public const int MaxPathLength = 260;
    public const int DefaultStringLength = 64;

    public const int PointerSize32 = 4;
    public const int PointerSize64 = 8;

    public const byte X64CallAllocStack = 0x28;

    public const int DefaultPollingIntervalMs = 100;
    public const int DefaultTimeoutMs = 10000;

    public static readonly string[] MonoModuleNames =
    [
        "mono-2.0-bdwgc.dll",
        "mono-2.0-sgen.dll",
        "mono.dll"
    ];

    public static class MonoExports
    {
        public const string RootDomain = "mono_get_root_domain";
        public const string ThreadAttach = "mono_thread_attach";

        public const string DomainAssemblyOpen = "mono_domain_assembly_open";
        public const string AssemblyGetImage = "mono_assembly_get_image";

        public const string ClassFromName = "mono_class_from_name";
        public const string ClassGetName = "mono_class_get_name";
        public const string ClassGetParent = "mono_class_get_parent";
        public const string ObjectGetClass = "mono_object_get_class";
        public const string ObjectNew = "mono_object_new";
        public const string ClassVTable = "mono_class_vtable";

        public const string ClassGetFieldFromName = "mono_class_get_field_from_name";
        public const string FieldGetOffset = "mono_field_get_offset";
        public const string FieldStaticGetValue = "mono_field_static_get_value";
        public const string FieldStaticSetValue = "mono_field_static_set_value";

        public const string ClassGetMethodFromName = "mono_class_get_method_from_name";
        public const string RuntimeInvoke = "mono_runtime_invoke";

        public const string StringNew = "mono_string_new";
    }

    public static class Client
    {
        public const string AssemblyName = "Assembly-CSharp.dll";
        public const string ManagedFolder = "Managed";
        public const string DefaultSha256Url = "https://data.l7ungdz.id.vn/dragonboy/sha256.json";
        public const string DefaultVersion = "2.5.0";
        public const string DefaultSha256 = "73BC66D1B6F530FA6DEB95586549B2223A5C957279249F9986A819E31A4E4C3C";
    }
}
