import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Text, TextInput, View } from "react-native";
import { useApiClient } from "@/api/ApiContext";
import type { LoyaltyAccount } from "@/api/commerceContracts";
import type { CustomerSummary } from "@/api/managementContracts";
import { managerStyles } from "@/components/managerStyles";
import { EmptyState, ScreenHeader } from "@/components/operations";
import { AppButton, LoadingSurface, Screen, textStyles } from "@/components/primitives";
import { createCommerceExtensions } from "@/services/commerceExtensions";
import { createManagerBusiness } from "@/services/managerBusiness";
import { useWorkspace } from "@/state/workspace";

export function LoyaltyScreen(){
 const router=useRouter(),client=useApiClient(),commerce=useMemo(()=>createCommerceExtensions(client),[client]),manager=useMemo(()=>createManagerBusiness(client),[client]);
 const {workspace}=useWorkspace(); const [accounts,setAccounts]=useState<readonly LoyaltyAccount[]>([]),[customers,setCustomers]=useState<readonly CustomerSummary[]>([]),[busy,setBusy]=useState(false),[message,setMessage]=useState("");
 const [selected,setSelected]=useState<LoyaltyAccount|null>(null),[points,setPoints]=useState("100"),[reason,setReason]=useState("Manual loyalty adjustment");
 const load=useCallback(async(signal?:AbortSignal)=>{setBusy(true);try{const [a,c]=await Promise.all([commerce.listLoyaltyAccounts(workspace.organizationId,100,signal),manager.listCustomers(workspace.organizationId,100,signal)]);setAccounts(a.items);setCustomers(c.items);setSelected(current=>current? a.items.find(x=>x.id===current.id)??null: null);setMessage("");}catch{setMessage("Loyalty data could not be loaded.");}finally{setBusy(false)}},[commerce,manager,workspace.organizationId]);
 useEffect(()=>{const c=new AbortController();void load(c.signal);return()=>c.abort()},[load]);
 async function open(customer:CustomerSummary){setBusy(true);try{await commerce.openLoyaltyAccount(workspace.organizationId,customer.id);await load();}catch{setMessage("Loyalty account could not be opened.");}finally{setBusy(false)}}
 async function change(kind:"earn"|"redeem"){if(!selected)return;setBusy(true);try{const result=await commerce.changeLoyaltyPoints(workspace.organizationId,selected,kind,Number(points),reason);setSelected(result.account);await load();setMessage(result.applied?`${kind} applied.`:`${kind} replayed safely.`);}catch{setMessage(`Points could not ${kind}.`)}finally{setBusy(false)}}
 const accountCustomerIds=new Set(accounts.map(x=>x.customerId));
 return <Screen><ScreenHeader title="Loyalty" onBack={()=>router.back()}/><Text style={textStyles.body}>Open customer accounts, track tiers and manage point balances.</Text>
 {message?<View style={managerStyles.card}><Text style={managerStyles.muted}>{message}</Text></View>:null}
 <View style={managerStyles.card}><Text style={textStyles.heading}>Customers without loyalty</Text>{customers.filter(c=>!accountCustomerIds.has(c.id)).slice(0,20).map(customer=><View key={customer.id} style={managerStyles.card}><Text style={managerStyles.strong}>{customer.displayName}</Text><Text style={managerStyles.mono}>{customer.code}</Text><AppButton disabled={busy} onPress={()=>void open(customer)}>Open loyalty account</AppButton></View>)}</View>
 {selected?<View style={managerStyles.card}><Text style={textStyles.heading}>Selected account</Text><Text style={managerStyles.strong}>{selected.pointsBalance} points · {selected.tier}</Text><Text style={managerStyles.muted}>Lifetime {selected.lifetimePoints} · v{selected.version}</Text><View style={managerStyles.field}><Text style={managerStyles.label}>Points</Text><TextInput value={points} onChangeText={setPoints} keyboardType="number-pad" style={managerStyles.input}/></View><View style={managerStyles.field}><Text style={managerStyles.label}>Reason</Text><TextInput value={reason} onChangeText={setReason} maxLength={200} style={managerStyles.input}/></View><AppButton disabled={busy} onPress={()=>void change("earn")}>Earn</AppButton><AppButton disabled={busy} onPress={()=>void change("redeem")}>Redeem</AppButton></View>:null}
 {busy?<LoadingSurface/>:null}{!busy&&accounts.length===0?<EmptyState message="No loyalty accounts were returned."/>:null}
 {accounts.map(account=><View key={account.id} style={managerStyles.card}><Text style={managerStyles.strong}>{account.pointsBalance} points · {account.tier}</Text><Text style={managerStyles.mono}>Customer {account.customerId.slice(0,8).toUpperCase()}</Text><Text style={managerStyles.muted}>Lifetime {account.lifetimePoints} · v{account.version}</Text><AppButton disabled={busy} onPress={()=>setSelected(account)}>Manage points</AppButton></View>)}</Screen>;
}
