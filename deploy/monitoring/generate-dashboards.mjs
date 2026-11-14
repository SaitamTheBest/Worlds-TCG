// Regenerate the provisioned dashboards with: node deploy/monitoring/generate-dashboards.mjs
import { writeFileSync, mkdirSync } from 'node:fs';
const directory=new URL('./grafana/dashboards/',import.meta.url);
mkdirSync(directory,{recursive:true});
function dashboard(uid,title,definitions) {
  const panels=definitions.map(([name,expr,type='timeseries',unit='short'],i)=>({
    id:i+1,title:name,type,gridPos:{x:(i%2)*12,y:Math.floor(i/2)*8,w:12,h:8},
    datasource:{type:'prometheus',uid:'prometheus'},targets:[{refId:'A',expr,legendFormat:'{{job}} {{instance}} {{container_label_com_docker_compose_service}} {{datname}} {{operation}} {{status}} {{code}} {{state}} {{device}}'}],
    fieldConfig:{defaults:{unit},overrides:[]},options:{legend:{displayMode:'table',placement:'bottom'},tooltip:{mode:'multi'}}
  }));
  writeFileSync(new URL(`${uid}.json`,directory),JSON.stringify({uid,title,schemaVersion:39,version:1,editable:false,tags:['worlds-tcg'],timezone:'browser',refresh:'30s',time:{from:'now-6h',to:'now'},panels},null,2)+'\n');
}
dashboard('worlds-overview','Worlds TCG · Vue générale',[
  ['Collecteurs disponibles','up','stat'],
  ['Disponibilité HTTPS','probe_success','stat'],
  ['CPU du VPS','1 - avg(rate(node_cpu_seconds_total{mode="idle"}[5m]))','timeseries','percentunit'],
  ['RAM du VPS','1 - node_memory_MemAvailable_bytes/node_memory_MemTotal_bytes','timeseries','percentunit'],
  ['Disque disponible','node_filesystem_avail_bytes{mountpoint="/",fstype!~"tmpfs|overlay|squashfs"}','timeseries','bytes'],
  ['Alertes actives','ALERTS{alertstate="firing"}','stat'],
  ['Joueurs inscrits','worlds_tcg_users','stat'],
  ['Parties en cours','worlds_tcg_active_matches','stat'],
  ['Enchères en cours','worlds_tcg_active_auctions','stat'],
  ['Cartes possédées (exemplaires)','worlds_tcg_collection_cards','stat'],
]);
dashboard('worlds-application','Worlds TCG · Application',[
  ['Requêtes par seconde','sum by (code) (rate(http_requests_received_total[5m]))','timeseries','reqps'],
  ['Latence p95','histogram_quantile(0.95,sum by(le)(rate(http_request_duration_seconds_bucket[5m])))','timeseries','s'],
  ['Erreurs 5xx par seconde','sum(rate(http_requests_received_total{code=~"5.."}[5m]))','timeseries','reqps'],
  ['Mémoire du processus .NET','process_working_set_bytes{job="application"}','timeseries','bytes'],
  ['Requêtes en base par seconde','sum by(operation,status)(rate(worlds_tcg_db_commands_total[5m]))','timeseries','ops'],
  ['Durée p95 des requêtes en base','histogram_quantile(0.95,sum by(le,operation)(rate(worlds_tcg_db_command_duration_seconds_bucket[5m])))','timeseries','s'],
  ['Collections du GC','rate(dotnet_collection_count_total{job="application"}[5m])','timeseries','ops'],
  ['Actualisation des indicateurs métier','time() - worlds_tcg_business_metrics_last_success_timestamp_seconds','stat','s'],
]);
dashboard('worlds-postgres','Worlds TCG · PostgreSQL',[
  ['Connexion à PostgreSQL','pg_up','stat'],
  ['Taille des bases','pg_database_size_bytes','timeseries','bytes'],
  ['Connexions par état','sum by(state)(pg_stat_activity_count)'],
  ['Transactions par seconde','sum by(datname)(rate(pg_stat_database_xact_commit[5m]))','timeseries','ops'],
  ['Transactions annulées par seconde','sum by(datname)(rate(pg_stat_database_xact_rollback[5m]))','timeseries','ops'],
  ['Taux de succès du cache','sum(rate(pg_stat_database_blks_hit[5m]))/clamp_min(sum(rate(pg_stat_database_blks_hit[5m])+rate(pg_stat_database_blks_read[5m])),0.001)','timeseries','percentunit'],
  ['Conflits de verrouillage (deadlocks)','sum by(datname)(rate(pg_stat_database_deadlocks[5m]))','timeseries','ops'],
  ['Fichiers temporaires','sum by(datname)(rate(pg_stat_database_temp_bytes[5m]))','timeseries','Bps'],
]);
dashboard('worlds-containers','Worlds TCG · VPS et conteneurs',[
  ['RAM par conteneur','container_memory_working_set_bytes{container_label_com_docker_compose_service!=""}','timeseries','bytes'],
  ['CPU par conteneur','sum by(container_label_com_docker_compose_service)(rate(container_cpu_usage_seconds_total{container_label_com_docker_compose_service!=""}[5m]))','timeseries','percentunit'],
  ['Réception réseau VPS','rate(node_network_receive_bytes_total{device!="lo"}[5m])','timeseries','Bps'],
  ['Émission réseau VPS','rate(node_network_transmit_bytes_total{device!="lo"}[5m])','timeseries','Bps'],
  ['Charge système','node_load1'],
  ['Swap utilisé','node_memory_SwapTotal_bytes-node_memory_SwapFree_bytes','timeseries','bytes'],
  ['Date de démarrage des conteneurs','container_start_time_seconds{container_label_com_docker_compose_service!=""}','stat','dateTimeAsIso'],
  ['Validité des certificats (jours restants)','(probe_ssl_earliest_cert_expiry-time())/86400'],
]);
const logs={uid:'worlds-logs',title:'Worlds TCG · Journaux',schemaVersion:39,version:1,editable:false,tags:['worlds-tcg'],refresh:'30s',time:{from:'now-1h',to:'now'},templating:{list:[{name:'service',type:'query',datasource:{type:'loki',uid:'loki'},query:'label_values(service)',refresh:1,includeAll:true,allValue:'.*',current:{text:'All',value:'$__all'}}]},panels:[{id:1,title:'Journaux Docker · sélectionner un service',type:'logs',gridPos:{x:0,y:0,w:24,h:24},datasource:{type:'loki',uid:'loki'},targets:[{refId:'A',expr:'{job="worlds-tcg", service=~"$service"}'}],options:{showTime:true,showLabels:true,sortOrder:'Descending',wrapLogMessage:true}}]};
writeFileSync(new URL('worlds-logs.json',directory),JSON.stringify(logs,null,2)+'\n');
