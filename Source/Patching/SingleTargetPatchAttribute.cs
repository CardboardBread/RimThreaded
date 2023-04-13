using RimThreaded.Utilities;
using System;
using System.Reflection;

namespace RimThreaded.Patching
{
    // Base pattern for harmony-style attributes that target some named member, and perform an operation between the
    // named member and the attribute's declaring member.
    public abstract class SingleTargetPatchAttribute : Attribute, ILocationAware
    {
        protected bool isLocating = false;
        protected bool isLocated = false;

        protected SingleTargetPatchAttribute()
        {
        }

        protected SingleTargetPatchAttribute(Type declaringType)
        {
            DeclaringType = declaringType;
        }

        protected SingleTargetPatchAttribute(string memberName)
        {
            MemberName = memberName;
        }

        protected SingleTargetPatchAttribute(Type declaringType, string memberName) : this(declaringType)
        {
            MemberName = memberName;
        }

        public Type DeclaringType { get; set; }
        public string MemberName { get; set; }
        public MethodType? MethodType { get; set; }
        public Type[] ArgumentTypes { get; set; }

        public virtual MemberInfo Parent { get; internal set; }
        public virtual MemberInfo Target { get; internal set; }

        internal virtual Type ResolveDeclaringType() => DeclaringType ?? Parent.HarmonyDeclaringType() ?? null;
        internal virtual string ResolveMemberName() => MemberName ?? Parent.HarmonyMethodName() ?? Parent.Name ?? null;
        internal virtual MethodType? ResolveMethodType() => MethodType ?? Parent.HarmonyMethodType() ?? null;
        internal virtual Type[] ResolveArgumentTypes() => ArgumentTypes ?? Parent.HarmonyArgumentTypes() ?? null;
        internal virtual MemberInfo ResolveTarget() => Target ?? null;

        public virtual bool IsLocated() => isLocated;
        internal virtual bool HasTarget() => IsLocated() && Target is not null;
        internal virtual bool IsTargetMissing() => IsLocated() && IsTargetReferenced() && Target is null;
        internal virtual bool IsTargetReferenced() => ResolveDeclaringType() != null && ResolveMemberName() != null;

        public virtual void Locate(MemberInfo parent)
        {
            try
            {
                isLocating = true;
                Parent = parent ?? throw new ArgumentNullException(nameof(parent));
                Target = ResolveTarget();
                isLocated = true;
            }
            finally
            {
                isLocating = false;
            }
        }

        protected virtual void ManualPatch(Harmony harmony)
        {
            throw new NotImplementedException();
        }
    }

    public abstract class SingleTargetPatchAttribute<T> : SingleTargetPatchAttribute where T : MemberInfo
    {
        protected SingleTargetPatchAttribute()
        {
        }

        protected SingleTargetPatchAttribute(Type declaringType) : base(declaringType)
        {
        }

        protected SingleTargetPatchAttribute(string memberName) : base(memberName)
        {
        }

        protected SingleTargetPatchAttribute(Type declaringType, string memberName) : base(declaringType, memberName)
        {
        }

        internal virtual new T Target
        {
            get => base.Target as T;
            set => base.Target = value;
        }

        public override void Locate(MemberInfo parent)
        {
            base.Locate(parent);

            if (base.Target is not T)
            {
                throw new ArgumentException(nameof(Target));
            }
        }

        internal override bool HasTarget() => IsLocated() && Target is not null;

        internal override bool IsTargetMissing() => IsLocated() && IsTargetReferenced() && Target is null;
    }

    public abstract class SingleTargetPatchAttribute<P, T> : SingleTargetPatchAttribute<T> where P : MemberInfo where T : MemberInfo
    {
        internal virtual new P Parent
        {
            get => base.Parent as P ?? throw new AttributeUsageException(nameof(Parent));
            set => base.Parent = value;
        }

        public override void Locate(MemberInfo member)
        {
            if (member is not P)
            {
                throw new AttributeUsageException(nameof(Parent));
            }

            base.Locate(member);
        }
    }
}
