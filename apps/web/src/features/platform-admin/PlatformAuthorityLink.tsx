"use client";
import Link from"next/link";
import{useEffect,useState}from"react";
import{getPlatformAuthority}from"./api";

export function PlatformAuthorityLink(){
  const[visible,setVisible]=useState(false);
  useEffect(()=>{const controller=new AbortController();getPlatformAuthority(controller.signal)
    .then(value=>setVisible(value.isSuperAdmin)).catch(()=>{});return()=>controller.abort()},[]);
  return visible?<Link className="secondary-button" href="/super-admin/overview">Super Admin</Link>:null
}
