import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Text, TextInput, View } from "react-native";
import { useApiClient } from "@/api/ApiContext";
import type { LocalizationSettings } from "@/api/globalConfigurationContracts";
import { managerStyles } from "@/components/managerStyles";
import { ScreenHeader } from "@/components/operations";
import { AppButton, LoadingSurface, Screen, textStyles } from "@/components/primitives";
import { createGlobalConfiguration } from "@/services/globalConfiguration";
import { useWorkspace } from "@/state/workspace";

export function LocalizationScreen(){
 const router=useRouter(),client=useApiClient(),service=useMemo(()=>createGlobalConfiguration(client),[client]);
 const {workspace}=useWorkspace();const [current,setCurrent]=useState<LocalizationSettings|null>(null),[busy,setBusy]=useState(false),[message,setMessage]=useState("");
 const [country,setCountry]=useState(""),[locale,setLocale]=useState(""),[currency,setCurrency]=useState(""),[zone,setZone]=useState(""),[locales,setLocales]=useState(""),[day,setDay]=useState("1");
 const apply=useCallback((value:LocalizationSettings|null)=>{setCurrent(value);setCountry(value?.countryCode??"");setLocale(value?.defaultLocale??"");setCurrency(value?.defaultCurrency??"");setZone(value?.timeZone??"");setLocales(value?.supportedLocales.join(", ")??"");setDay(String(value?.firstDayOfWeek??1));},[]);
 const load=useCallback(async(signal?:AbortSignal)=>{setBusy(true);try{apply(await service.readLocalization(workspace.organizationId,signal));setMessage("");}catch{setMessage("Localization settings are not configured or could not be loaded.");}finally{setBusy(false)}},[apply,service,workspace.organizationId]);
 useEffect(()=>{const c=new AbortController();void load(c.signal);return()=>c.abort()},[load]);
 async function save(){setBusy(true);setMessage("");try{const supported=locales.split(",").map(x=>x.trim()).filter(Boolean);const result=await service.updateLocalization(workspace.organizationId,{countryCode:country,defaultLocale:locale,defaultCurrency:currency,timeZone:zone,supportedLocales:supported,firstDayOfWeek:Number(day),expectedVersion:current?.version??null});apply(result.settings);setMessage(result.applied?"Localization settings saved.":"The same operation was already applied.");}catch{setMessage("Localization settings could not be saved.");}finally{setBusy(false)}}
 return <Screen><ScreenHeader title="Localization" onBack={()=>router.back()}/><Text style={textStyles.body}>Configure organization country, language, currency and timezone defaults.</Text>
 {message?<View style={managerStyles.card}><Text style={managerStyles.muted}>{message}</Text></View>:null}
 <View style={managerStyles.card}>
  <View style={managerStyles.field}><Text style={managerStyles.label}>Country code</Text><TextInput value={country} onChangeText={x=>setCountry(x.toUpperCase())} maxLength={2} style={managerStyles.input}/></View>
  <View style={managerStyles.field}><Text style={managerStyles.label}>Default locale</Text><TextInput value={locale} onChangeText={setLocale} maxLength={35} style={managerStyles.input}/></View>
  <View style={managerStyles.field}><Text style={managerStyles.label}>Default currency</Text><TextInput value={currency} onChangeText={x=>setCurrency(x.toUpperCase())} maxLength={3} style={managerStyles.input}/></View>
  <View style={managerStyles.field}><Text style={managerStyles.label}>Timezone</Text><TextInput value={zone} onChangeText={setZone} maxLength={100} style={managerStyles.input}/></View>
  <View style={managerStyles.field}><Text style={managerStyles.label}>Supported locales</Text><TextInput value={locales} onChangeText={setLocales} style={managerStyles.input}/></View>
  <View style={managerStyles.field}><Text style={managerStyles.label}>First day of week</Text><TextInput value={day} onChangeText={setDay} keyboardType="number-pad" style={managerStyles.input}/></View>
  <AppButton disabled={busy} onPress={()=>void save()}>Save localization</AppButton>
 </View>{busy?<LoadingSurface/>:null}</Screen>;
}
