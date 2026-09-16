import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Text, TextInput, View } from "react-native";
import { useApiClient } from "@/api/ApiContext";
import type { TaxCalculation, TaxProfile, TaxRate } from "@/api/globalConfigurationContracts";
import { managerStyles } from "@/components/managerStyles";
import { ScreenHeader } from "@/components/operations";
import { AppButton, LoadingSurface, Screen, textStyles } from "@/components/primitives";
import { createGlobalConfiguration } from "@/services/globalConfiguration";
import { useWorkspace } from "@/state/workspace";

export function TaxationScreen(){
 const router=useRouter(),client=useApiClient(),service=useMemo(()=>createGlobalConfiguration(client),[client]);
 const {workspace}=useWorkspace(),branch=workspace.branch;const [profiles,setProfiles]=useState<readonly TaxProfile[]>([]),[rates,setRates]=useState<readonly TaxRate[]>([]);
 const [selected,setSelected]=useState(""),[busy,setBusy]=useState(false),[message,setMessage]=useState("");
 const [code,setCode]=useState("STANDARD"),[name,setName]=useState("Standard tax"),[country,setCountry]=useState("GE"),[inclusive,setInclusive]=useState(true);
 const [category,setCategory]=useState("STANDARD"),[rate,setRate]=useState("18"),[amount,setAmount]=useState("100"),[calculation,setCalculation]=useState<TaxCalculation|null>(null);
 const load=useCallback(async(signal?:AbortSignal)=>{setBusy(true);try{const page=await service.listTaxProfiles(workspace.organizationId,signal);setProfiles(page.items);setSelected(current=>page.items.some(x=>x.id===current)?current:(page.items[0]?.id??""));setMessage("");}catch{setMessage("Tax profiles could not be loaded.");}finally{setBusy(false)}},[service,workspace.organizationId]);
 useEffect(()=>{const c=new AbortController();void load(c.signal);return()=>c.abort()},[load]);
 useEffect(()=>{if(!selected){setRates([]);return;}const c=new AbortController();void service.listTaxRates(workspace.organizationId,selected,c.signal).then(setRates).catch(()=>setMessage("Tax rates could not be loaded."));return()=>c.abort()},[selected,service,workspace.organizationId]);
 async function createProfile(){setBusy(true);try{const created=await service.createTaxProfile(workspace.organizationId,{code,name,countryCode:country,pricesIncludeTax:inclusive});await load();setSelected(created.id);}catch{setMessage("Tax profile could not be created.");}finally{setBusy(false)}}
 async function createRate(){if(!selected)return;setBusy(true);try{await service.createTaxRate(workspace.organizationId,selected,{...(branch?{branchId:branch.id}:{}),categoryCode:category,ratePercent:Number(rate),effectiveFrom:new Date().toISOString()});setRates(await service.listTaxRates(workspace.organizationId,selected));}catch{setMessage("Tax rate could not be created.");}finally{setBusy(false)}}
 async function calculate(){if(!selected||!branch)return;setBusy(true);try{setCalculation(await service.calculateTax(workspace.organizationId,{profileId:selected,branchId:branch.id,categoryCode:category,amount:Number(amount),at:new Date().toISOString()}));setMessage("");}catch{setMessage("Tax calculation failed.");}finally{setBusy(false)}}
 return <Screen><ScreenHeader title="Taxation" onBack={()=>router.back()}/><Text style={textStyles.body}>Manage effective tax profiles and calculate the tax split for the selected store.</Text>
 {message?<View style={managerStyles.card}><Text style={managerStyles.muted}>{message}</Text></View>:null}{busy?<LoadingSurface/>:null}
 <View style={managerStyles.card}><Text style={textStyles.heading}>Profile</Text><View style={managerStyles.field}><Text style={managerStyles.label}>Code</Text><TextInput value={code} onChangeText={x=>setCode(x.toUpperCase())} maxLength={40} style={managerStyles.input}/></View><View style={managerStyles.field}><Text style={managerStyles.label}>Name</Text><TextInput value={name} onChangeText={setName} maxLength={120} style={managerStyles.input}/></View><View style={managerStyles.field}><Text style={managerStyles.label}>Country</Text><TextInput value={country} onChangeText={x=>setCountry(x.toUpperCase())} maxLength={2} style={managerStyles.input}/></View><AppButton disabled={busy} onPress={()=>setInclusive(x=>!x)}>Prices include tax: {inclusive?"Yes":"No"}</AppButton><AppButton disabled={busy||!code||!name} onPress={()=>void createProfile()}>Create profile</AppButton></View>
 <View style={managerStyles.card}><Text style={textStyles.heading}>Profiles</Text>{profiles.map(p=><AppButton key={p.id} disabled={busy} onPress={()=>setSelected(p.id)}>{p.id===selected?"✓ ":""}{p.code} · {p.name}</AppButton>)}</View>
 <View style={managerStyles.card}><Text style={textStyles.heading}>Effective rate</Text><View style={managerStyles.field}><Text style={managerStyles.label}>Category</Text><TextInput value={category} onChangeText={x=>setCategory(x.toUpperCase())} maxLength={40} style={managerStyles.input}/></View><View style={managerStyles.field}><Text style={managerStyles.label}>Rate %</Text><TextInput value={rate} onChangeText={setRate} keyboardType="decimal-pad" style={managerStyles.input}/></View><AppButton disabled={busy||!selected} onPress={()=>void createRate()}>Add rate</AppButton>{rates.map(r=><Text key={r.id} style={managerStyles.muted}>{r.categoryCode} · {r.ratePercent}% · {r.branchId?"store":"organization"}</Text>)}</View>
 <View style={managerStyles.card}><Text style={textStyles.heading}>Calculator</Text><View style={managerStyles.field}><Text style={managerStyles.label}>Amount</Text><TextInput value={amount} onChangeText={setAmount} keyboardType="decimal-pad" style={managerStyles.input}/></View><AppButton disabled={busy||!selected||!branch} onPress={()=>void calculate()}>Calculate tax</AppButton>{calculation?<Text style={managerStyles.strong}>Net {calculation.netAmount.toFixed(2)} · Tax {calculation.taxAmount.toFixed(2)} · Gross {calculation.grossAmount.toFixed(2)}</Text>:null}</View>
 </Screen>;
}
