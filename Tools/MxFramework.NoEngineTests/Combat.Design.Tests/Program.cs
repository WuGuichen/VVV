using System.Text.Json;
using MxFramework.Authoring;

var tests = new (string, Action)[] {
    ("strict schema rejects duplicate and unknown fields", () => WithFixture((g, h, hash, root) => {
        Throws(() => g.Submit("{\"operation\":\"refresh\",\"operation\":\"refresh\",\"idempotencyKey\":\"x\"}"), "DUPLICATE_FIELD");
        Throws(() => g.Submit("{\"operation\":\"refresh\",\"idempotencyKey\":\"x\",\"sourcePath\":\"x\"}"), "UNKNOWN_FIELD");
    })),
    ("typed patch bounds and design rationale", () => WithFixture((g,h,hash,root) => {
        Throws(() => g.Plan(Plan(h, hash, 0)), "INVALID_FACING_PATCH");
        Throws(() => g.Plan(Plan(h, hash, 181)), "INVALID_FACING_PATCH");
        var p = Element(g.Plan(Plan(h,hash,8)));
        Equal(2,p.GetProperty("plan").GetProperty("affectedCards").GetArrayLength());
    })),
    ("snapshot CAS and source-code invalidation", () => WithFixture((g,h,hash,root) => {
        Throws(() => g.Plan(Plan(h,new string('0',64),8)), "SNAPSHOT_CONFLICT");
        File.WriteAllText(Path.Combine(root,"source.cs"),"changed compiler");
        Throws(() => g.Plan(Plan(h,hash,8)), "SOURCE_CHANGED");
        var q=Element(g.Query()); Equal(false,q.GetProperty("snapshots")[0].GetProperty("sourceCurrent").GetBoolean());
        Equal(false,q.GetProperty("snapshots")[0].GetProperty("evidenceCurrent").GetBoolean());
    })),
    ("ten retries queue exactly one command and survive stale snapshot", () => WithFixture((g,h,hash,root) => {
        string token=Element(g.Plan(Plan(h,hash,8))).GetProperty("planToken").GetString();
        string request=JsonSerializer.Serialize(new {operation="commit",idempotencyKey="edit1",planToken=token});
        string id=Element(g.Submit(request)).GetProperty("operationId").GetString();
        File.WriteAllText(Path.Combine(root,"source.cs"),"after commit");
        for(int i=0;i<10;i++) Equal(id,Element(g.Submit(request)).GetProperty("operationId").GetString());
        Equal(1,Directory.GetFiles(Path.Combine(root,"Temp/MxFramework/CombatDesign/requests"),"*.json").Length);
    })),
    ("same key different payload conflicts", () => WithFixture((g,h,hash,root) => {
        g.Submit("{\"operation\":\"refresh\",\"idempotencyKey\":\"one\"}");
        Throws(() => g.Submit("{\"operation\":\"create-fixture\",\"idempotencyKey\":\"one\"}"),"IDEMPOTENCY_CONFLICT");
        Equal(1,Directory.GetFiles(Path.Combine(root,"Temp/MxFramework/CombatDesign/requests"),"*.json").Length);
    })),
    ("JSON field order is idempotent", () => WithFixture((g,h,hash,root) => {
        var a=Element(g.Submit("{\"operation\":\"refresh\",\"idempotencyKey\":\"one\"}"));
        var b=Element(g.Submit("{\"idempotencyKey\":\"one\",\"operation\":\"refresh\"}"));
        Equal(a.GetProperty("operationId").GetString(),b.GetProperty("operationId").GetString());
    })),
    ("pending is not successful owner evidence", () => WithFixture((g,h,hash,root) => {
        var command=Element(g.Submit("{\"operation\":\"create-fixture\",\"idempotencyKey\":\"create\"}"));
        Equal("PendingEditor",command.GetProperty("status").GetString());
        string id=command.GetProperty("operationId").GetString();
        string dir=Path.Combine(root,"Temp/MxFramework/CombatDesign/responses"); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir,id+".json"),"{\"status\":\"RecoveryRequired\"}");
        Equal("RecoveryRequired",Element(g.Status(id)).GetProperty("status").GetString());
    })),
    ("path handles cannot traverse and read-only snapshots cannot write", () => WithFixture((g,h,hash,root) => {
        Throws(() => g.Status("../outside"),"INVALID_HANDLE");
        string file=Path.Combine(root,"Temp/MxFramework/CombatDesign/snapshots",h+".json");
        string readOnly=File.ReadAllText(file).Replace("\"writable\":true","\"writable\":false");File.WriteAllText(file,readOnly);
        Throws(() => g.Plan(Plan(h,CombatDesignGateway.Hash(readOnly),8)),"READ_ONLY");
    }))
};
int failed=0;
foreach(var (name,test) in tests) { try {test();Console.WriteLine("PASS "+name);} catch(Exception ex){failed++;Console.Error.WriteLine("FAIL "+name+": "+ex);} }
Console.WriteLine($"{tests.Length-failed}/{tests.Length} passed; portable gateway evidence only, Unity owner/Play not run.");
return failed==0?0:1;

static JsonElement Element(object value)=>JsonSerializer.SerializeToElement(value);
static string Plan(string h,string hash,float threshold)=>JsonSerializer.Serialize(new{handle=h,snapshotHash=hash,thresholdDegrees=threshold,maximumFrames=45,rationale="Agent design choice, not inferred from code"});
static void Equal<T>(T expected,T actual){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}, actual {actual}");}
static void Throws(Action action,string code){try{action();}catch(Exception ex)when(ex.Message.Contains(code)){return;}throw new Exception("Expected error "+code);}
static void WithFixture(Action<CombatDesignGateway,string,string,string> test){
    string root=Path.Combine(Path.GetTempPath(),"combat-design-gateway-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
    try{
        string file=Path.Combine(root,"source.cs"); File.WriteAllText(file,"compiler v1");
        string h=CombatDesignGateway.Hash("owner-generated-handle");
        string snapshot=JsonSerializer.Serialize(new{handle=h,writable=true,sourceFingerprint="owner-hash",evidence=new{sourceFingerprint="owner-hash"},
            readSet=new[]{new{path="source.cs",hash=CombatDesignGateway.Hash(File.ReadAllBytes(file))}},
            cards=new[]{new{dependsOnFacing=true},new{dependsOnFacing=true},new{dependsOnFacing=false}}});
        string dir=Path.Combine(root,"Temp/MxFramework/CombatDesign/snapshots");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,h+".json"),snapshot);
        test(new CombatDesignGateway(root),h,CombatDesignGateway.Hash(snapshot),root);
    }finally{Directory.Delete(root,true);}
}
