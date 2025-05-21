using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace EmailSender.ContractResolvers
{
    public class IgnoreSensitiveDataContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            if (member.Name == "SenderEmailPassword" ||
                member.Name == "SenderName" ||
                member.Name == "HostSmtp" ||
                member.Name == "Port")
            {
                property.ShouldSerialize = _ => false;
            }

            return property;
        }
    }
}