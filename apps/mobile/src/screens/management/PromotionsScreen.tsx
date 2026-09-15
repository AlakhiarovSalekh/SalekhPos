import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Text, TextInput, View } from "react-native";
import { useApiClient } from "@/api/ApiContext";
import type { Promotion, PromotionEvaluation } from "@/api/commerceContracts";
import { managerStyles } from "@/components/managerStyles";
import { EmptyState, ScreenHeader } from "@/components/operations";
import { AppButton, LoadingSurface, Screen, textStyles } from "@/components/primitives";
import { createCommerceExtensions } from "@/services/commerceExtensions";
import { useWorkspace } from "@/state/workspace";

export function PromotionsScreen(){
 const router=useRouter(),client=useApiClient(),commerce=useMemo(()=>createCommerceExtensions(client),[client]);
 const {workspace}=useWorkspace(),branch=workspace.branch; const [items,setItems]=useState<readonly Promotion[]>([]),[busy,setBusy]=useState(false),[message,setMessage]=useState("");
 const [code,setCode]=useState(""),[name,setName]=useState(""),[value,setValue]=useState("10"),[minimum,setMinimum]=useState("0"),[currency,setCurrency]=useState("GEL"),[fixed,setFixed]=useState(false);
 const [subtotal,setSubtotal]=useState("100"),[evaluation,setEvaluation]=useState<PromotionEvaluation|null>(null);
 const load=useCallback(async(signal?:AbortSignal)=>{if(!branch){setItems([]);return;}setBusy(true);try{setItems((await commerce.listPromotions(workspace.organizationId,branch.id,100,signal)).items);setMessage("");}catch{setMessage("Promotions could not be loaded.");}finally{setBusy(false)}},[branch,commerce,workspace.organizationId]);
 useEffect(()=>{const c=new AbortController();void load(c.signal);return()=>c.abort()},[load]);
 async function create(){if(!branch)return;setBusy(true);try{await commerce.createPromotion(workspace.organizationId,branch.id,{code,name,discountKind:fixed?"fixed":"percentage",value:Number(value),...(fixed?{currency}:{}),minimumSubtotal:Number(minimum),startsAt:new Date().toISOString()});setCode("");setName("");await load();}catch{setMessage("Promotion could not be created.");}finally{setBusy(false)}}
 async function deactivate(item:Promotion){if(!branch)return;setBusy(true);try{await commerce.deactivatePromotion(workspace.organizationId,branch.id,item);await load();}catch{setMessage("Promotion could not be deactivated.");}finally{setBusy(false)}}
 async function evaluate(){if(!branch)return;setBusy(true);try{setEvaluation(await commerce.evaluatePromotion(workspace.organizationId,branch.id,Number(subtotal),currency));setMessage("");}catch{setMessage("Promotion evaluation failed.");}finally{setBusy(false)}}
 return <Screen><ScreenHeader title="Promotions" onBack={()=>router.back()}/><Text style={textStyles.body}>Create branch campaigns and preview the effective discount.</Text>
 {!branch?<View style={managerStyles.warning}><Text style={managerStyles.strong}>Store required</Text><Text style={managerStyles.muted}>Choose a store first.</Text></View>:null}
 {message?<View style={managerStyles.card}><Text style={managerStyles.muted}>{message}</Text></View>:null}
 {branch?<View style={managerStyles.card}><Text style={textStyles.heading}>New campaign</Text>
  <View style={managerStyles.field}><Text style={managerStyles.label}>Code</Text><TextInput value={code} onChangeText={x=>setCode(x.toUpperCase())} maxLength={40} style={managerStyles.input}/></View>
  <View style={managerStyles.field}><Text style={managerStyles.label}>Name</Text><TextInput value={name} onChangeText={setName} maxLength={160} style={managerStyles.input}/></View>
  <View style={managerStyles.field}><Text style={managerStyles.label}>Discount value</Text><TextInput value={value} onChangeText={setValue} keyboardType="decimal-pad" style={managerStyles.input}/></View>
  <View style={managerStyles.field}><Text style={managerStyles.label}>Minimum subtotal</Text><TextInput value={minimum} onChangeText={setMinimum} keyboardType="decimal-pad" style={managerStyles.input}/></View>
  <View style={managerStyles.field}><Text style={managerStyles.label}>Currency</Text><TextInput value={currency} onChangeText={x=>setCurrency(x.toUpperCase())} maxLength={3} style={managerStyles.input}/></View>
  <AppButton onPress={()=>setFixed(x=>!x)}>{fixed?"Fixed amount":"Percentage"}</AppButton><AppButton disabled={busy||!code||!name} onPress={()=>void create()}>Create campaign</AppButton></View>:null}
 {branch?<View style={managerStyles.card}><Text style={textStyles.heading}>Evaluate cart</Text><View style={managerStyles.field}><Text style={managerStyles.label}>Subtotal</Text><TextInput value={subtotal} onChangeText={setSubtotal} keyboardType="decimal-pad" style={managerStyles.input}/></View><AppButton disabled={busy} onPress={()=>void evaluate()}>Evaluate</AppButton>{evaluation?<Text style={managerStyles.strong}>Discount {evaluation.totalDiscount.toFixed(2)} {evaluation.currency} Â· Payable {evaluation.payable.toFixed(2)}</Text>:null}</View>:null}
 {busy?<LoadingSurface/>:null}{!busy&&branch&&items.length===0?<EmptyState message="No promotions were returned."/>:null}
 {items.map(item=><View key={item.id} style={managerStyles.card}><Text style={managerStyles.strong}>{item.code} Â· {item.name}</Text><Text style={managerStyles.muted}>{item.discountKind} {item.value}{item.discountKind==="percentage"?"%":` ${item.currency??""}`} Â· minimum {item.minimumSubtotal}</Text><Text style={managerStyles.muted}>{item.isActive?"Active":"Inactive"} Â· v{item.version}</Text>{item.isActive?<AppButton disabled={busy} onPress={()=>void deactivate(item)}>Deactivate</AppButton>:null}</View>)}</Screen>;
}

