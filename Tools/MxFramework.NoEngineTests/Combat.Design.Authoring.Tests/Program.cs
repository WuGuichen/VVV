using System.Text.Json;
using MxFramework.Authoring;

string root = Path.Combine(Path.GetTempPath(), "combat-authoring-gateway-" + Guid.NewGuid().ToString("N"));
string transport = Path.Combine(root, "Temp/MxFramework/CombatDesign/authoring");
Directory.CreateDirectory(transport);
var gateway = new CombatDesignAuthoringGateway(root);
int checks = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
bool Reject(Action action, string code) { try { action(); return false; } catch (Exception e) { return e.Message.Contains(code); } }
JsonElement As(object value) => JsonSerializer.SerializeToElement(value);
string Request(string operation, string key = "key", int cost = 1) => JsonSerializer.Serialize(new { operation, idempotencyKey=key, request=new { scope="main", cost } });
Check(!As(gateway.Query()).GetProperty("ready").GetBoolean(), "Missing owner is offline");
Check(Reject(()=>gateway.Submit(Request("save")), "OWNER_UNAVAILABLE"), "Offline write is not queued");
File.WriteAllText(Path.Combine(transport,"owner.json"),JsonSerializer.Serialize(new { ready=true,updatedUtc=DateTime.UtcNow.ToString("O") }));
var queued=As(gateway.Submit(Request("save"))); string id=queued.GetProperty("operationId").GetString();
Check(queued.GetProperty("status").GetString()=="PendingEditor", "Submission is pending, never false success");
Check(As(gateway.Submit(Request("save"))).GetProperty("operationId").GetString()==id, "Identical duplicate keeps operation identity");
Check(Reject(()=>gateway.Submit(Request("save",cost:2)), "IDEMPOTENCY_CONFLICT"), "Different duplicate is rejected");
Directory.CreateDirectory(Path.Combine(transport,"responses"));
File.WriteAllText(Path.Combine(transport,"responses",id+".json"),JsonSerializer.Serialize(new {operationId=id,status="Succeeded",revision=2}));
File.Delete(Path.Combine(transport,"owner.json"));
Check(As(gateway.Submit(Request("save"))).GetProperty("revision").GetInt32()==2,"Completed result survives offline retry");
Check(Reject(()=>gateway.Status("../owner"),"INVALID_OPERATION_ID"),"Status path traversal rejected");
Check(Reject(()=>gateway.Submit(Request("create-real-card")),"UNKNOWN_AUTHORING_OPERATION"),"Unknown operations rejected");
Check(Reject(()=>gateway.Submit("{\"operation\":\"save\",\"operation\":\"publish\",\"idempotencyKey\":\"x\",\"request\":{}}"),"UNKNOWN_OR_DUPLICATE_FIELD"),"Duplicate envelope fields rejected");
File.WriteAllText(Path.Combine(transport,"owner.json"),JsonSerializer.Serialize(new {ready=true,updatedUtc=DateTime.UtcNow.AddMinutes(-1).ToString("O")}));
Check(!As(gateway.Query()).GetProperty("ready").GetBoolean(),"Stale heartbeat cannot authorize writes");
Check(!Directory.Exists(Path.Combine(root,"Authoring"))&&!Directory.Exists(Path.Combine(root,"Assets")),"Transport writes no author or production files");
Console.WriteLine($"{checks} authoring gateway checks passed; evidence: {root}");
