import { useRouter } from "expo-router";
import { useCallback,useEffect,useMemo,useState } from "react";
import { Text,TextInput,View } from "react-native";
import { useApiClient } from "@/api/ApiContext";
import type { AuditEvent,AuditIntegrity } from "@/api/auditContracts";
import { managerStyles } from "@/components/managerStyles";
import { EmptyState,ScreenHeader } from "@/components/operations";
import { AppButton,LoadingSurface,Screen,textStyles } from "@/components/primitives";
import { createAuditViewer } from "@/services/auditViewer";
import { useWorkspace } from "@/state/workspace";

export function AuditScreen(){
 const router=useRouter(),client=useApiClient(),audit=useMemo(()=>createAuditViewer(client),[client]); const {workspace}=useWorkspace();
 const [items,setItems]=useState<readonly AuditEvent[]>([]),[integrity,setIntegrity]=useState<AuditIntegrity|null>(null),[action,setAction]=useState(""),[busy,setBusy]=useState(false),[message,setMessage]=useState("");
 const load=useCallback(async(signal?:AbortSignal)=>{setBusy(true);try{const input:{pageSize:number;action?:string;branchId?:string}={pageSize:100};if(action)input.action=action;if(workspace.branch)input.branchId=workspace.branch.id;const page=await audit.list(workspace.organizationId,input,signal);setItems(page.items);setMessage("");}catch{setMessage("Audit events could not be loaded.");}finally{setBusy(false)}},[action,audit,workspace.branch,workspace.organizationId]);
 useEffect(()=>{const c=new AbortController();void load(c.signal);return()=>c.abort()},[load]);
 async function verify(){setBusy(true);try{setIntegrity(await audit.verify(workspace.organizationId));setMessage("");}catch{setMessage("Audit integrity verification failed.");}finally{setBusy(false)}}
 return <Screen><ScreenHeader title="Audit trail" onBack={()=>router.back()}/><Text style={textStyles.body}>Review immutable tenant activity evidence and verify the audit hash chain.</Text>
 <View style={managerStyles.card}><View style={managerStyles.field}><Text style={managerStyles.label}>Action filter</Text><TextInput value={action} onChangeText={setAction} maxLength={120} style={managerStyles.input}/></View><AppButton disabled={busy} onPress={()=>void load()}>Refresh</AppButton><AppButton disabled={busy} onPress={()=>void verify()}>Verify integrity</AppButton>{integrity?<Text style={integrity.isValid?managerStyles.strong:managerStyles.warning}>{integrity.isValid?"Integrity valid":"INTEGRITY FAILURE"} · {integrity.verifiedEvents} event(s)</Text>:null}</View>
 {message?<View style={managerStyles.card}><Text style={managerStyles.muted}>{message}</Text></View>:null}{busy?<LoadingSurface/>:null}{!busy&&items.length===0?<EmptyState message="No audit events were returned."/>:null}
 {items.map(item=><View key={item.id} style={managerStyles.card}><Text style={managerStyles.strong}>#{item.sequence} · {item.action}</Text><Text style={managerStyles.muted}>{item.actorSubject} · {item.outcome}</Text><Text style={managerStyles.muted}>{item.targetType}{item.targetId?` · ${item.targetId.slice(0,8)}`:""}</Text><Text style={managerStyles.mono}>{item.eventHash.slice(0,24)}…</Text></View>)}</Screen>;
}
