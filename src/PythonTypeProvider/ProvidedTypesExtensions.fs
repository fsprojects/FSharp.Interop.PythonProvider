module System.Reflection.MemberInfo

let inline addXmlDoc xmlDoc (memberInfo: #MemberInfo) = 
    (^T : (member AddXmlDoc: string -> unit) (memberInfo, xmlDoc))

