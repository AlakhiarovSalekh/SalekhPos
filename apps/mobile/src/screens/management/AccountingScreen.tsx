import { useRouter } from "expo-router";
import { useMemo, useState } from "react";
import { Text, View } from "react-native";
import { useApiClient } from "@/api/ApiContext";
import type { AccountingJournalItem, AccountingSummary } from "@/api/accountingContracts";
import { managerStyles } from "@/components/managerStyles";
import { AppButton, Screen, textStyles } from "@/components/primitives";
import { ScreenHeader } from "@/components/operations";
import { useLocalization } from "@/localization/LocalizationProvider";
import { createAccountingService } from "@/services/accounting";
import { mapSafeError, safeErrorTranslationKey } from "@/services/safeError";
import { useWorkspace } from "@/state/workspace";

export function AccountingScreen(){
 const router=useRouter(); const {t}=useLocalization(); const client=useApiClient();
 const accounting=useMemo(()=>createAccountingService(client),[client]); const {workspace}=useWorkspace();
 const [summary,setSummary]=useState<AccountingSummary|null>(null); const [journal,setJournal]=useState<readonly AccountingJournalItem[]>([]);
 const [nextCursor,setNextCursor]=useState<string|null>(null); const [loading,setLoading]=useState(false); const [message,setMessage]=useState("");
 const money=(value:number,currency:string|null)=>currency?`${currency} ${value.toFixed(2)}`:value.toFixed(2);
 async function load(){if(!workspace.branch)return;setLoading(true);setMessage("");const to=new Date(),from=new Date(to.getTime()-30*86_400_000);try{
  const [s,j]=await Promise.all([accounting.summary(workspace.organizationId,workspace.branch.id,from.toISOString(),to.toISOString()),accounting.journal(workspace.organizationId,workspace.branch.id,from.toISOString(),to.toISOString())]);
  setSummary(s);setJournal(j.items);setNextCursor(j.nextCursor);
 }catch(error){setMessage(t(safeErrorTranslationKey(mapSafeError(error))))}finally{setLoading(false)}}
 async function more(){if(!workspace.branch||!nextCursor||loading)return;setLoading(true);try{const to=new Date(),from=new Date(to.getTime()-30*86_400_000);const page=await accounting.journal(workspace.organizationId,workspace.branch.id,from.toISOString(),to.toISOString(),50,nextCursor);setJournal(current=>[...current,...page.items]);setNextCursor(page.nextCursor)}catch(error){setMessage(t(safeErrorTranslationKey(mapSafeError(error))))}finally{setLoading(false)}}
 return <Screen>
  <ScreenHeader title={t("management.accounting")} onBack={()=>router.back()}/>
  <Text style={textStyles.body}>Sales, tax, cash, purchase commitments and source journal evidence.</Text>
  {!workspace.branch?<View style={managerStyles.warning}><Text style={managerStyles.strong}>Store required</Text><Text style={managerStyles.muted}>Choose a store before loading accounting data.</Text></View>:null}
  {message?<View style={managerStyles.card}><Text style={managerStyles.muted}>{message}</Text></View>:null}
  {workspace.branch?<AppButton disabled={loading} onPress={()=>void load()}>{loading?"Loading…":"Load accounting"}</AppButton>:null}
  {summary?<>
   <View style={managerStyles.card}><Text style={managerStyles.strong}>Net receipts</Text><Text style={managerStyles.mono}>{money(summary.netReceipts,summary.currency)}</Text></View>
   <View style={managerStyles.card}><Text style={managerStyles.strong}>Sales & tax</Text><Text style={managerStyles.muted}>{money(summary.salesGross,summary.currency)} gross · {money(summary.salesTax,summary.currency)} tax · {summary.completedSales} sales</Text></View>
   <View style={managerStyles.card}><Text style={managerStyles.strong}>Refunds / voids</Text><Text style={managerStyles.muted}>{money(summary.refunds,summary.currency)} · {summary.completedReturns} returns</Text></View>
   <View style={managerStyles.card}><Text style={managerStyles.strong}>Cash movement</Text><Text style={managerStyles.muted}>{money(summary.cashIn-summary.cashOut,summary.currency)} net movement</Text></View>
   <View style={managerStyles.card}><Text style={managerStyles.strong}>Purchase commitments</Text><Text style={managerStyles.muted}>{summary.approvedPurchaseOrders} approved · {money(summary.purchaseCommitments,summary.currency)}</Text></View>
   <View style={managerStyles.card}><Text style={managerStyles.strong}>Shift variance</Text><Text style={managerStyles.muted}>{summary.closedShifts} closed · {money(summary.shiftVariance,summary.currency)}</Text></View>
  </>:null}
  {journal.length?<><Text style={textStyles.heading}>Source journal</Text>{journal.map(item=><View key={`${item.kind}:${item.sourceId}`} style={managerStyles.card}><Text style={managerStyles.strong}>{item.kind.replaceAll("_"," ")}</Text><Text style={managerStyles.muted}>{new Date(item.occurredAt).toLocaleString()} · {money(item.grossAmount,item.currency)} gross · {money(item.cashEffect,item.currency)} cash</Text><Text style={managerStyles.mono}>{item.sourceId}</Text></View>)}</>:null}
  {nextCursor?<AppButton disabled={loading} onPress={()=>void more()}>{loading?"Loading…":"Load more journal entries"}</AppButton>:null}
 </Screen>;
}
