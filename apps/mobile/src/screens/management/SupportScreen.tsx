import { randomUUID } from "expo-crypto";
import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Text, TextInput, View } from "react-native";
import { useApiClient } from "@/api/ApiContext";
import type { SupportPriority, SupportStatus, SupportTicket, SupportTicketDetail } from "@/api/supportContracts";
import { managerStyles } from "@/components/managerStyles";
import { EmptyState, ScreenHeader } from "@/components/operations";
import { AppButton, LoadingSurface, Screen, textStyles } from "@/components/primitives";
import { useLocalization } from "@/localization/LocalizationProvider";
import { hasPermission, permissions } from "@/permissions/policy";
import { createSupportService } from "@/services/support";
import { useSession } from "@/state/SessionContext";
import { useWorkspace } from "@/state/workspace";

type Intent={fingerprint:string;key:string};
function intent(parts:readonly unknown[],previous:Intent|null):Intent{const fingerprint=JSON.stringify(parts);return previous?.fingerprint===fingerprint?previous:{fingerprint,key:randomUUID()}}
function nextStatuses(status:SupportStatus):readonly SupportStatus[]{switch(status){case"open":return["in_progress","closed"];case"in_progress":return["waiting_for_customer","resolved","closed"];case"waiting_for_customer":return["in_progress","resolved","closed"];case"resolved":return["in_progress","closed"];case"closed":return[]}}

export function SupportScreen(){
 const router=useRouter(),client=useApiClient(),service=useMemo(()=>createSupportService(client),[client]),{t}=useLocalization();
 const {workspace}=useWorkspace(),{session}=useSession();const authorization=session?.authorization;
 const canCreate=authorization?hasPermission(authorization,permissions.supportCreate):false,canManage=authorization?hasPermission(authorization,permissions.supportManage):false;
 const [tickets,setTickets]=useState<readonly SupportTicket[]>([]),[nextCursor,setNextCursor]=useState<string|null>(null),[detail,setDetail]=useState<SupportTicketDetail|null>(null);
 const [filter,setFilter]=useState<SupportStatus|null>(null),[busy,setBusy]=useState(false),[message,setMessage]=useState("");
 const [subject,setSubject]=useState(""),[description,setDescription]=useState(""),[priority,setPriority]=useState<SupportPriority>("normal");
 const [nextStatus,setNextStatus]=useState<SupportStatus|"">(""),[note,setNote]=useState("");
 const [kind,setKind]=useState("log"),[reference,setReference]=useState(""),[sha,setSha]=useState("");
 const createIntent=useRef<Intent|null>(null),transitionIntent=useRef<Intent|null>(null),diagnosticIntent=useRef<Intent|null>(null);
 const load=useCallback(async(signal?:AbortSignal)=>{setBusy(true);try{const page=await service.list(workspace.organizationId,filter,null,signal);setTickets(page.items);setNextCursor(page.nextCursor);setMessage("");}catch{setMessage("Support tickets could not be loaded.");}finally{setBusy(false)}},[filter,service,workspace.organizationId]);
 useEffect(()=>{const c=new AbortController();void load(c.signal);return()=>c.abort()},[load]);
 async function select(ticket:SupportTicket){setBusy(true);try{const value=await service.get(workspace.organizationId,ticket.id);setDetail(value);setNextStatus(nextStatuses(value.ticket.status)[0]??"");setMessage("");}catch{setMessage("Support ticket could not be loaded.");}finally{setBusy(false)}}
 async function create(){const input={branchId:workspace.branch?.id??null,subject:subject.trim(),description:description.trim(),priority};if(!input.subject||!input.description)return;createIntent.current=intent(["create",workspace.organizationId,input],createIntent.current);setBusy(true);try{const ticket=await service.create(workspace.organizationId,input,createIntent.current.key);createIntent.current=null;setSubject("");setDescription("");await load();await select(ticket);}catch{setMessage("Ticket creation failed. Retrying unchanged input is safe.");}finally{setBusy(false)}}
 async function transition(){if(!detail||!nextStatus||!note.trim())return;const input={status:nextStatus,expectedVersion:detail.ticket.version,note:note.trim()};transitionIntent.current=intent(["transition",detail.ticket.id,input],transitionIntent.current);setBusy(true);try{const ticket=await service.transition(workspace.organizationId,detail.ticket.id,input,transitionIntent.current.key);transitionIntent.current=null;setNote("");await load();await select(ticket);}catch{setMessage("Ticket transition failed. Refresh before changing the request.");}finally{setBusy(false)}}
 async function addDiagnostic(){if(!detail||!kind.trim()||!reference.trim()||!/^[0-9a-fA-F]{64}$/u.test(sha.trim()))return;const input={kind:kind.trim(),reference:reference.trim(),sha256:sha.trim().toLowerCase()};diagnosticIntent.current=intent(["diagnostic",detail.ticket.id,input],diagnosticIntent.current);setBusy(true);try{await service.addDiagnostic(workspace.organizationId,detail.ticket.id,input,diagnosticIntent.current.key);diagnosticIntent.current=null;setReference("");setSha("");await select(detail.ticket);}catch{setMessage("Diagnostic reference could not be attached.");}finally{setBusy(false)}}
 async function more(){if(!nextCursor)return;setBusy(true);try{const page=await service.list(workspace.organizationId,filter,nextCursor);setTickets(current=>[...current,...page.items]);setNextCursor(page.nextCursor);}catch{setMessage("More support tickets could not be loaded.");}finally{setBusy(false)}}
 const allowed=detail?nextStatuses(detail.ticket.status):[];
 return <Screen><ScreenHeader title={t("management.support")} onBack={()=>router.back()}/><Text style={textStyles.body}>Track store incidents, controlled workflow changes and integrity-bound diagnostics.</Text>
 {message?<View style={managerStyles.card}><Text style={managerStyles.muted}>{message}</Text></View>:null}{busy?<LoadingSurface/>:null}
 <View style={managerStyles.card}><Text style={textStyles.heading}>Filter</Text><View style={managerStyles.row}><AppButton disabled={busy} onPress={()=>setFilter(null)}>{filter===null?"✓ ":""}All</AppButton>{(["open","in_progress","waiting_for_customer","resolved","closed"] as const).map(value=><AppButton key={value} disabled={busy} onPress={()=>setFilter(value)}>{filter===value?"✓ ":""}{value.replaceAll("_"," ")}</AppButton>)}</View></View>
 {canCreate?<View style={managerStyles.card}><Text style={textStyles.heading}>Create ticket</Text><Text style={managerStyles.muted}>{workspace.branch?"Store: "+workspace.branch.name:"Organization-wide ticket"}</Text><View style={managerStyles.field}><Text style={managerStyles.label}>Subject</Text><TextInput value={subject} onChangeText={setSubject} maxLength={200} style={managerStyles.input}/></View><View style={managerStyles.field}><Text style={managerStyles.label}>Description</Text><TextInput value={description} onChangeText={setDescription} maxLength={8000} multiline style={managerStyles.input}/></View><View style={managerStyles.row}>{(["low","normal","high","urgent"] as const).map(value=><AppButton key={value} disabled={busy} onPress={()=>setPriority(value)}>{priority===value?"✓ ":""}{value}</AppButton>)}</View><AppButton disabled={busy||!subject.trim()||!description.trim()} onPress={()=>void create()}>Create ticket</AppButton></View>:null}
 <Text style={textStyles.heading}>Tickets</Text>{!busy&&tickets.length===0?<EmptyState message="No support tickets were returned."/>:null}
 {tickets.map(ticket=><View key={ticket.id} style={managerStyles.card}><Text style={managerStyles.strong}>{ticket.subject}</Text><Text style={managerStyles.muted}>{ticket.priority} · {ticket.status.replaceAll("_"," ")} · v{ticket.version}</Text><Text style={managerStyles.mono}>{ticket.id}</Text><AppButton disabled={busy} onPress={()=>void select(ticket)}>Open ticket</AppButton></View>)}
 {nextCursor?<AppButton disabled={busy} onPress={()=>void more()}>Load more tickets</AppButton>:null}
 {detail?<View style={managerStyles.card}><Text style={textStyles.heading}>Ticket detail</Text><Text style={managerStyles.strong}>{detail.ticket.subject}</Text><Text style={managerStyles.muted}>{detail.ticket.description}</Text><Text style={managerStyles.muted}>Opened by {detail.ticket.openedBySubject} · {new Date(detail.ticket.updatedAt).toLocaleString()}</Text>
 {canManage&&allowed.length?<><Text style={managerStyles.strong}>Transition</Text><View style={managerStyles.row}>{allowed.map(value=><AppButton key={value} disabled={busy} onPress={()=>setNextStatus(value)}>{nextStatus===value?"✓ ":""}{value.replaceAll("_"," ")}</AppButton>)}</View><View style={managerStyles.field}><Text style={managerStyles.label}>Note</Text><TextInput value={note} onChangeText={setNote} maxLength={2000} multiline style={managerStyles.input}/></View><AppButton disabled={busy||!nextStatus||!note.trim()} onPress={()=>void transition()}>Apply transition</AppButton></>:null}
 <Text style={managerStyles.strong}>Diagnostics</Text>{detail.diagnostics.map(item=><View key={item.id} style={managerStyles.section}><Text style={managerStyles.muted}>{item.kind} · {item.reference}</Text><Text style={managerStyles.mono}>{item.sha256}</Text></View>)}
 {canManage?<><View style={managerStyles.field}><Text style={managerStyles.label}>Kind</Text><TextInput value={kind} onChangeText={setKind} maxLength={64} style={managerStyles.input}/></View><View style={managerStyles.field}><Text style={managerStyles.label}>Reference</Text><TextInput value={reference} onChangeText={setReference} maxLength={512} style={managerStyles.input}/></View><View style={managerStyles.field}><Text style={managerStyles.label}>SHA-256</Text><TextInput value={sha} onChangeText={setSha} maxLength={64} autoCapitalize="none" style={managerStyles.input}/></View><AppButton disabled={busy||!kind.trim()||!reference.trim()||!/^[0-9a-fA-F]{64}$/u.test(sha.trim())} onPress={()=>void addDiagnostic()}>Attach diagnostic</AppButton></>:null}
 </View>:null}</Screen>;
}
