using System;
using LamarCodeGeneration;
using Marten.Internal.Linq;
using Marten.Schema;

namespace Marten.Internal.CodeGeneration
{
    public class SelectorBuilder
    {
        private readonly DocumentMapping _mapping;
        private readonly StorageStyle _style;

        public SelectorBuilder(DocumentMapping mapping, StorageStyle style)
        {
            _mapping = mapping;
            _style = style;
        }

        public GeneratedType BuildType(GeneratedAssembly assembly)
        {
            var typeName = $"{_style}{_mapping.DocumentType.Name.Sanitize()}Selector";


            var baseType = determineBaseType();

            var type = assembly.AddType(typeName, baseType);
            var interfaceType = typeof(ISelector<>).MakeGenericType(_mapping.DocumentType);
            type.Implements(interfaceType);

            var sync = type.MethodFor("Resolve");
            var async = type.MethodFor("ResolveAsync");

            var versionPosition = _mapping.IsHierarchy() ? 3 : 2;

            switch (_style)
            {
                case StorageStyle.QueryOnly:
                    sync.Frames.Deserialize(_mapping);
                    async.Frames.DeserializeAsync(_mapping);
                    break;

                case StorageStyle.IdentityMap:
                    sync.Frames.GetId(_mapping);
                    async.Frames.GetIdAsync(_mapping);

                    sync.Frames.CheckExistingFirst();
                    async.Frames.CheckExistingFirst();

                    sync.Frames.Deserialize(_mapping);
                    async.Frames.DeserializeAsync(_mapping);

                    sync.Frames.MarkAsLoaded();
                    async.Frames.MarkAsLoaded();

                    sync.Frames.StoreVersion(false, _mapping, versionPosition);
                    async.Frames.StoreVersion(true, _mapping, versionPosition);

                    sync.Frames.StoreInIdentityMap(_mapping);
                    async.Frames.StoreInIdentityMap(_mapping);

                    break;

                case StorageStyle.DirtyTracking:
                    sync.Frames.GetId(_mapping);
                    async.Frames.GetIdAsync(_mapping);

                    sync.Frames.CheckExistingFirst();
                    async.Frames.CheckExistingFirst();

                    sync.Frames.Deserialize(_mapping);
                    async.Frames.DeserializeAsync(_mapping);

                    sync.Frames.MarkAsLoaded();
                    async.Frames.MarkAsLoaded();

                    sync.Frames.StoreVersion(false, _mapping, versionPosition);
                    async.Frames.StoreVersion(true, _mapping, versionPosition);

                    sync.Frames.StoreInIdentityMap(_mapping);
                    async.Frames.StoreInIdentityMap(_mapping);

                    sync.Frames.StoreTracker();
                    async.Frames.StoreTracker();

                    break;

                case StorageStyle.Lightweight:
                    sync.Frames.GetId(_mapping);
                    async.Frames.GetIdAsync(_mapping);

                    sync.Frames.Deserialize(_mapping);
                    async.Frames.DeserializeAsync(_mapping);

                    sync.Frames.MarkAsLoaded();
                    async.Frames.MarkAsLoaded();


                    sync.Frames.StoreVersion(false, _mapping, versionPosition);
                    async.Frames.StoreVersion(true, _mapping, versionPosition);

                    break;
                default:
                    throw new InvalidOperationException();
            }


            sync.Frames.Return(_mapping.DocumentType);
            if (_style == StorageStyle.QueryOnly && !_mapping.IsHierarchy())
            {
                async.Frames.Code("return Task.FromResult(document);");
            }
            else
            {
                async.Frames.Return(_mapping.DocumentType);
            }

            return type;
        }

        private Type determineBaseType()
        {
            switch (_style)
            {
                case StorageStyle.QueryOnly:
                    return typeof(DocumentSelectorWithOnlySerializer);

                case StorageStyle.IdentityMap:
                    return typeof(DocumentSelectorWithIdentityMap<,>)
                        .MakeGenericType(_mapping.DocumentType, _mapping.IdType);

                case StorageStyle.Lightweight:
                    return typeof(DocumentSelectorWithVersions<,>)
                        .MakeGenericType(_mapping.DocumentType, _mapping.IdType);

                case StorageStyle.DirtyTracking:
                    return typeof(DocumentSelectorWithDirtyChecking<,>)
                        .MakeGenericType(_mapping.DocumentType, _mapping.IdType);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
