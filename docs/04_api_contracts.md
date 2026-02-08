# API Contracts

## Customers
POST /customers  
Body: `{ "name": string, "phone": string }`  
Response: `{ "customerId": guid, "name": string, "phone": string }`

GET /customers?query=...  
Response: `{ "customers": [{ "customerId": guid, "name": string, "phone": string }] }`

---

## Orders
POST /orders  
Body: `{ "customerId": guid }`  
Response: `{ "orderId": guid, "customerId": guid, "status": "Open" }`

POST /orders/{id}/cylinders  
Body: `{ "cylinderId"?: guid }` (se não fornecido, cria novo)  
Response: `{ "cylinderId": guid, "orderId": guid, "state": "Received" }`

POST /orders/{id}/cylinders/scan  
Body: `{ "qrToken": string }`  
Response: `{ "cylinderId": guid, "orderId": guid, "state": "Received" }`

---

## Cylinders
POST /cylinders/{id}/assign-label  
Body: `{ "qrToken": string }`  
Response: `{ "cylinderId": guid, "labelToken": string }`

POST /cylinders/scan  
Body: `{ "qrToken": string }`  
Response: `{ "cylinderId": guid, "customerId": guid, "orderId": guid, "state": string, "progress": { "ready": int, "total": int } }`

POST /cylinders/{id}/mark-ready  
Response: `{ "cylinderId": guid, "state": "Ready", "orderReady": bool }`

POST /cylinders/{id}/report-problem  
Body: `{ "type": string, "notes": string }`  
Response: `{ "cylinderId": guid, "state": "Problem" }`

---

## Print Jobs
POST /print-jobs  
Body: `{ "storeId": guid, "qty": int, "templateId"?: string }`  
Response: `{ "printJobId": guid, "status": "Pending" }`

POST /print-jobs/{id}/ack-printed  
Response: `{ "printJobId": guid, "status": "Printed" }`

POST /print-jobs/{id}/ack-failed  
Body: `{ "error": string }`  
Response: `{ "printJobId": guid, "status": "Failed" }`

---

## SignalR Hubs
### PrintHub
- Server → Gateway: `PrintJobCreated` (jobId, qty, templateId)
- Gateway → Server: via API acima (ack-printed/ack-failed)
