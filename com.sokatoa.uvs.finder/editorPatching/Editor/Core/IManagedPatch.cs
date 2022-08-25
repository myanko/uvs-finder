using System;

namespace needle.EditorPatching
{
    public interface IManagedPatch
    {
        string Id { get; }
        bool IsActive { get; }
        void EnablePatch(bool forceSync = false);
        void DisablePatch();
        string Name { get; }
        string Description { get; }
        string Group { get; }
    }

    public static class PatchHelpers
    {
        public static string GetId(Type type) => type?.FullName;
    }
}