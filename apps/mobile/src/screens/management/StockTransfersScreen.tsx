import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Text, TextInput, View } from "react-native";
import { useApiClient } from "@/api/ApiContext";
import type { StockTransfer } from "@/api/commerceContracts";
import { AppButton, LoadingSurface, Screen, textStyles } from "@/components/primitives";
import { EmptyState, ScreenHeader } from "@/components/operations";
import { managerStyles } from "@/components/managerStyles";
import { createCommerceExtensions } from "@/services/commerceExtensions";
import { useWorkspace } from "@/state/workspace";

export function StockTransfersScreen(){
 const router=useRouter(),client=useApiClient(),commerce=useMemo(()=>createCommerceExtensions(client),[client]);
 const {workspace}=useWorkspace(); const branch=workspace.branch;
 const [items,setItems]=useState<readonly StockTransfer[]>([]),[busy,setBusy]=useState(false),[message,setMessage]=useState("");
 const [destination,setDestination]=useState(""),[product,setProduct]=useState(""),[quantity,setQuantity]=useState("1"),[reference,setReference]=useState("");
 const load=useCallback(async(signal?:AbortSignal)=>{if(!branch){setItems([]);return;}setBusy(true);try{setItems((await commerce.listTransfers(workspace.organizationId,branch.id,100,signal)).items);setMessage("");}catch{setMessage("Transfers could not be loaded.");}finally{setBusy(false)}},[branch,commerce,workspace.organizationId]);
 useEffect(()=>{const c=new AbortController();void load(c.signal);return()=>c.abort()},[load]);
 async function create(){if(!branch)return;setBusy(true);try{await commerce.createTransfer(workspace.organizationId,branch.id,{destinationBranchId:destination,reference,lines:[{productId:product,quantity:Number(quantity)}]});setDestination("");setProduct("");setReference("");await load();}catch{setMessage("Transfer could not be created.");}finally{setBusy(false)}}
 async function change(item:StockTransfer,action:"dispatch"|"receive"|"cancel"){if(!branch)return;setBusy(true);try{await commerce.changeTransfer(workspace.organizationId,branch.id,item,action);await load();}catch{setMessage(`Transfer could not ${action}.`)}finally{setBusy(false)}}
 return <Screen><ScreenHeader title="Stock transfers" onBack={()=>router.back()}/><Text style={textStyles.body}>Move stock between stores with dispatch and receiving evidence.</Text>
 {!branch?<View style={managerStyles.warning}><Text style={managerStyles.strong}>Store required</Text><Text style={managerStyles.muted}>Choose a store first.</Text></View>:null}
 {message?<View style={managerStyles.card}><Text style={managerStyles.muted}>{message}</Text></View>:null}
 {branch?<View style={managerStyles.card}><Text style={textStyles.heading}>Create transfer</Text>
  <View style={managerStyles.field}><Text style={managerStyles.label}>Destination branch ID</Text><TextInput value={destination} onChangeText={setDestination} autoCapitalize="none" style={managerStyles.input}/></View>
  <View style={managerStyles.field}><Text style={managerStyles.label}>Product ID</Text><TextInput value={product} onChangeText={setProduct} autoCapitalize="none" style={managerStyles.input}/></View>
  <View style={managerStyles.field}><Text style={managerStyles.label}>Quantity</Text><TextInput value={quantity} onChangeText={setQuantity} keyboardType="decimal-pad" style={managerStyles.input}/></View>
  <View style={managerStyles.field}><Text style={managerStyles.label}>Reference</Text><TextInput value={reference} onChangeText={setReference} maxLength={120} style={managerStyles.input}/></View>
  <AppButton disabled={busy||!destination||!product} onPress={()=>void create()}>Create draft</AppButton></View>:null}
 {busy?<LoadingSurface/>:null}{!busy&&branch&&items.length===0?<EmptyState message="No transfers were returned."/>:null}
 {items.map(item=><View key={item.id} style={managerStyles.card}><Text style={managerStyles.strong}>{item.reference??item.id.slice(0,8).toUpperCase()} · {item.status}</Text><Text style={managerStyles.mono}>{item.sourceBranchId.slice(0,8)} → {item.destinationBranchId.slice(0,8)}</Text><Text style={managerStyles.muted}>{item.lines.length} line(s) · v{item.version}</Text>
  {branch?.id===item.sourceBranchId&&item.status==="draft"?<AppButton disabled={busy} onPress={()=>void change(item,"dispatch")}>Dispatch</AppButton>:null}
  {branch?.id===item.destinationBranchId&&item.status==="in_transit"?<AppButton disabled={busy} onPress={()=>void change(item,"receive")}>Receive</AppButton>:null}
  {branch?.id===item.sourceBranchId&&item.status==="draft"?<AppButton disabled={busy} onPress={()=>void change(item,"cancel")}>Cancel</AppButton>:null}
 </View>)}</Screen>;
}
