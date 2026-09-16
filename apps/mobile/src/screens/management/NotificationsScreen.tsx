import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Text, View } from "react-native";
import { useApiClient } from "@/api/ApiContext";
import type { NotificationItem, NotificationPreferences } from "@/api/globalConfigurationContracts";
import { managerStyles } from "@/components/managerStyles";
import { EmptyState, ScreenHeader } from "@/components/operations";
import { AppButton, LoadingSurface, Screen, textStyles } from "@/components/primitives";
import { createGlobalConfiguration } from "@/services/globalConfiguration";
import { useWorkspace } from "@/state/workspace";

export function NotificationsScreen(){
 const router=useRouter(),client=useApiClient(),service=useMemo(()=>createGlobalConfiguration(client),[client]);
 const {workspace}=useWorkspace();const [items,setItems]=useState<readonly NotificationItem[]>([]),[prefs,setPrefs]=useState<NotificationPreferences|null>(null);
 const [unreadOnly,setUnreadOnly]=useState(false),[busy,setBusy]=useState(false),[message,setMessage]=useState("");
 const load=useCallback(async(signal?:AbortSignal)=>{setBusy(true);try{const [page,p]=await Promise.all([service.listNotifications(workspace.organizationId,unreadOnly,signal),service.readNotificationPreferences(workspace.organizationId,signal)]);setItems(page.items);setPrefs(p);setMessage("");}catch{setMessage("Notifications could not be loaded.");}finally{setBusy(false)}},[service,unreadOnly,workspace.organizationId]);
 useEffect(()=>{const c=new AbortController();void load(c.signal);return()=>c.abort()},[load]);
 async function markRead(item:NotificationItem){setBusy(true);try{await service.markNotificationRead(workspace.organizationId,item.id);await load();}catch{setMessage("Notification could not be marked as read.");}finally{setBusy(false)}}
 async function save(next:NotificationPreferences){setBusy(true);try{setPrefs(await service.updateNotificationPreferences(workspace.organizationId,next));setMessage("");}catch{setMessage("Notification preferences could not be saved.");}finally{setBusy(false)}}
 return <Screen><ScreenHeader title="Notifications" onBack={()=>router.back()}/><Text style={textStyles.body}>Review operational alerts and choose delivery channels.</Text>
 {message?<View style={managerStyles.card}><Text style={managerStyles.muted}>{message}</Text></View>:null}
 <View style={managerStyles.card}><Text style={textStyles.heading}>Inbox</Text><AppButton disabled={busy} onPress={()=>setUnreadOnly(x=>!x)}>{unreadOnly?"Show all":"Unread only"}</AppButton></View>
 {busy?<LoadingSurface/>:null}{!busy&&items.length===0?<EmptyState message="No notifications were returned."/>:null}
 {items.map(item=><View key={item.id} style={managerStyles.card}><Text style={managerStyles.strong}>{item.title}</Text><Text style={managerStyles.muted}>{item.severity} · {new Date(item.createdAt).toLocaleString()}</Text><Text style={managerStyles.muted}>{item.body}</Text>{!item.isRead?<AppButton disabled={busy} onPress={()=>void markRead(item)}>Mark read</AppButton>:null}</View>)}
 {prefs?<View style={managerStyles.card}><Text style={textStyles.heading}>Preferences</Text><AppButton disabled={busy} onPress={()=>void save({...prefs,inAppEnabled:!prefs.inAppEnabled})}>In-app: {prefs.inAppEnabled?"On":"Off"}</AppButton><AppButton disabled={busy} onPress={()=>void save({...prefs,emailEnabled:!prefs.emailEnabled})}>Email: {prefs.emailEnabled?"On":"Off"}</AppButton><AppButton disabled={busy} onPress={()=>void save({...prefs,pushEnabled:!prefs.pushEnabled})}>Push: {prefs.pushEnabled?"On":"Off"}</AppButton></View>:null}
 </Screen>;
}
