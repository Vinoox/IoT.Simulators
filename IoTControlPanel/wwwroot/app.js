const API_BASE = '/api';

// Funkcja pobierająca stan początkowy po załadowaniu strony
async function fetchServicesState() {
    try {
        const response = await fetch(`${API_BASE}/registry/services`);
        if (!response.ok) throw new Error('Błąd pobierania danych');

        const services = await response.json();
        renderServices(services);
    } catch (error) {
        console.error('Błąd:', error);
        showAlert('Nie można nawiązać połączenia z rejestrem serwisów.', 'danger');
    }
}

// Główna funkcja renderująca - inteligentnie aktualizuje tylko zmienione elementy
function renderServices(services) {
    // Blokada odświeżania, jeśli użytkownik właśnie coś wpisuje w polach input
    const activeElement = document.activeElement;
    if (activeElement && (activeElement.tagName === 'INPUT' || activeElement.tagName === 'SELECT')) {
        return;
    }

    const simGrid = document.getElementById('simulatorsGrid');
    const infraGrid = document.getElementById('infraGrid');
    const loadingSpinner = document.getElementById('loadingSpinner');

    if (loadingSpinner) loadingSpinner.remove();

    // Filtrowanie usług na infrastrukturę (Broker/Collector) i Symulatory
    let simCount = 0;
    let infCount = 0;

    services.forEach(service => {
        const isRunning = service.isRunning;
        const currentProtocol = (service.protocol || "HTTP").toUpperCase();
        const isInfra = service.serviceId.includes('Broker') || service.serviceId.includes('Collector');
        const protocolClass = currentProtocol.includes('HTTP') ? 'badge-http' : 'badge-mqtt';

        if (isInfra) infCount++; else simCount++;

        const existingCard = document.getElementById(`card-${service.serviceId}`);

        if (existingCard) {
            // --- AKTUALIZACJA ISTNIEJĄCEJ KARTY (Szybka, bez migotania) ---

            // 1. Licznik pakietów
            const msgsBadge = document.getElementById(`msgs-${service.serviceId}`);
            if (msgsBadge && msgsBadge.innerText !== (service.processedMessages || 0).toString()) {
                msgsBadge.innerText = service.processedMessages || 0;
            }

            // 2. Czas ostatniej aktualizacji
            const timeSpan = document.getElementById(`time-${service.serviceId}`);
            if (timeSpan) timeSpan.innerText = new Date(service.lastUpdated).toLocaleTimeString();

            // 3. Status (kropka i plakietka) - aktualizowane tylko przy zmianie stanu
            const statusDiv = document.getElementById(`status-${service.serviceId}`);
            if (statusDiv) {
                const lastState = statusDiv.getAttribute('data-last-state');
                const currentState = isRunning.toString();

                if (lastState !== currentState) {
                    statusDiv.setAttribute('data-last-state', currentState);
                    statusDiv.innerHTML = isRunning
                        ? `<div class="d-flex align-items-center"><span class="status-dot"></span><span class="badge bg-success bg-opacity-10 text-success border border-success border-opacity-25 px-2 py-1">${isInfra ? 'ONLINE' : 'NADAJE'}</span></div>`
                        : `<div class="d-flex align-items-center"><span class="status-dot stopped"></span><span class="badge bg-danger bg-opacity-10 text-danger border border-danger border-opacity-25 px-2 py-1">${isInfra ? 'OFFLINE' : 'WSTRZYMANY'}</span></div>`;

                    // Aktualizacja przycisku sterującego
                    const toggleBtn = document.getElementById(`btn-toggle-${service.serviceId}`);
                    if (toggleBtn) {
                        toggleBtn.className = `btn btn-${isRunning ? 'danger' : 'success'} shadow-sm py-2 text-uppercase fw-bold`;
                        toggleBtn.innerHTML = isRunning ? '<i class="bi bi-stop-circle-fill me-1"></i> Zatrzymaj' : '<i class="bi bi-play-circle-fill me-1"></i> Uruchom';
                        toggleBtn.onclick = () => toggleState(service.serviceId, isRunning);
                    }
                }
            }
        } else {
            // --- TWORZENIE NOWEJ KARTY (Pierwszy render) ---
            const card = document.createElement('div');
            card.id = `card-${service.serviceId}`;

            if (isInfra) {
                card.className = 'col-12 mb-4';
                card.innerHTML = `
                    <div class="card h-100 bg-dark text-white border-secondary shadow-lg">
                        <div class="card-header border-bottom border-secondary border-opacity-25 d-flex justify-content-between align-items-center">
                            <h6 class="mb-0 fw-bold text-white tracking-tight"><i class="bi bi-hdd-rack-fill text-info me-2"></i>${service.serviceId.toUpperCase()}</h6>
                            <div id="status-${service.serviceId}" data-last-state="${isRunning}">
                                ${isRunning ? '<span class="status-dot"></span> ONLINE' : '<span class="status-dot stopped"></span> OFFLINE'}
                            </div>
                        </div>
                        <div class="card-body p-3 d-flex flex-column">
                            <div class="bg-secondary bg-opacity-25 border border-secondary rounded p-3 mb-3 small">
                                <p class="mb-2 d-flex justify-content-between"><span>Protokół:</span> <span class="badge ${protocolClass}">${service.protocol}</span></p>
                                <p class="mb-2 d-flex justify-content-between"><span>Host:</span> <strong>${service.targetAddress}</strong></p>
                                <p class="mb-0 d-flex justify-content-between"><span>Punkt styku:</span> <strong>${service.topicOrPath}</strong></p>
                            </div>
                            <div class="mt-auto bg-black border border-success border-opacity-25 rounded p-2 d-flex justify-content-between align-items-center">
                                <span class="text-white-50 small">Pakiety:</span>
                                <span class="badge bg-success text-dark fs-6" id="msgs-${service.serviceId}">${service.processedMessages || 0}</span>
                            </div>
                        </div>
                        <div class="card-footer bg-transparent pt-0 border-0 text-white-50 text-end small">
                            Aktualizacja: <span id="time-${service.serviceId}">${new Date(service.lastUpdated).toLocaleTimeString()}</span>
                        </div>
                    </div>`;
                infraGrid.appendChild(card);
            } else {
                card.className = 'col-md-6 col-xxl-4 mb-4';
                card.innerHTML = `
                    <div class="card h-100 border-0 shadow-sm">
                        <div class="card-header bg-transparent d-flex justify-content-between align-items-center p-3">
                            <h5 class="mb-0 fw-bold">${service.serviceId.toUpperCase()}</h5>
                            <div id="status-${service.serviceId}" data-last-state="${isRunning}">
                                ${isRunning ? '<span class="status-dot"></span>' : '<span class="status-dot stopped"></span>'}
                            </div>
                        </div>
                        <div class="card-body d-flex flex-column p-3">
                            <div class="d-flex justify-content-between align-items-center mb-3">
                                <span class="badge ${protocolClass} px-3 py-1 rounded-pill shadow-sm">${service.protocol}</span>
                            </div>
                            <div class="bg-light border p-2 mb-3 rounded">
                                <select class="form-select form-select-sm mb-1" id="protocol-${service.serviceId}">
                                    <option value="HTTP" ${currentProtocol === 'HTTP' ? 'selected' : ''}>HTTP</option>
                                    <option value="MQTT" ${currentProtocol === 'MQTT' ? 'selected' : ''}>MQTT</option>
                                </select>
                                <input type="text" class="form-control form-control-sm mb-1" id="target-${service.serviceId}" value="${service.targetAddress}">
                                <input type="text" class="form-control form-control-sm mb-2" id="topic-${service.serviceId}" value="${service.topicOrPath}">
                                <button class="btn btn-outline-dark btn-sm w-100" onclick="updateConfig('${service.serviceId}')">Zastosuj sieć</button>
                            </div>
                            <div class="input-group input-group-sm mb-3">
                                <span class="input-group-text">Interwał (ms)</span>
                                <input type="number" class="form-control" id="interval-${service.serviceId}" value="${service.intervalMilliseconds}">
                                <button class="btn btn-primary" onclick="updateInterval('${service.serviceId}')">OK</button>
                            </div>
                            <div class="d-grid mt-auto">
                                <button id="btn-toggle-${service.serviceId}" class="btn btn-${isRunning ? 'danger' : 'success'} py-2 fw-bold" onclick="toggleState('${service.serviceId}', ${isRunning})">
                                    ${isRunning ? 'Zatrzymaj' : 'Uruchom'}
                                </button>
                            </div>
                        </div>
                    </div>`;
                simGrid.appendChild(card);
            }
        }
    });

    document.getElementById('simulatorsCount').innerText = simCount;
    document.getElementById('infraCount').innerText = infCount;
}

// --- FUNKCJE STERUJĄCE (Wysłanie komend do serwera) ---

async function toggleState(serviceId, isRunning) {
    const action = isRunning ? 'stop' : 'start';
    try {
        const response = await fetch(`${API_BASE}/control/${action}?service=${serviceId}`, { method: 'POST' });
        if (!response.ok) throw new Error();
        showAlert(`Serwis ${serviceId}: polecenie ${action} wysłane.`, 'success');
    } catch {
        showAlert(`Błąd podczas próby zmiany stanu ${serviceId}.`, 'danger');
    }
}

async function updateInterval(serviceId) {
    const ms = document.getElementById(`interval-${serviceId}`).value;
    try {
        const response = await fetch(`${API_BASE}/control/update-interval?service=${serviceId}&intervalMs=${ms}`, { method: 'POST' });
        if (!response.ok) throw new Error();
        showAlert(`Zmieniono interwał dla ${serviceId} na ${ms}ms.`, 'success');
    } catch {
        showAlert('Błąd aktualizacji interwału.', 'danger');
    }
}

async function updateConfig(serviceId) {
    const protocol = document.getElementById(`protocol-${serviceId}`).value;
    const target = document.getElementById(`target-${serviceId}`).value;
    const topic = document.getElementById(`topic-${serviceId}`).value;

    const endpoint = protocol === 'HTTP' ? 'switch-to-http' : 'switch-to-mqtt';
    const params = protocol === 'HTTP'
        ? `targetAddress=${encodeURIComponent(target)}&apiPath=${encodeURIComponent(topic)}`
        : `brokerAddress=${encodeURIComponent(target)}&topic=${encodeURIComponent(topic)}`;

    try {
        const response = await fetch(`${API_BASE}/control/${endpoint}?service=${serviceId}&${params}`, { method: 'POST' });
        if (!response.ok) throw new Error();
        showAlert(`Zaktualizowano konfigurację sieciową ${serviceId}.`, 'success');
    } catch {
        showAlert('Błąd zmiany protokołu.', 'danger');
    }
}

function showAlert(message, type) {
    const container = document.getElementById('alertsContainer');
    const div = document.createElement('div');
    div.className = `alert alert-${type} alert-dismissible fade show`;
    div.innerHTML = `${message}<button type="button" class="btn-close" data-bs-dismiss="alert"></button>`;
    container.appendChild(div);
    setTimeout(() => div.remove(), 4000);
}

// --- KONFIGURACJA SIGNALR ---

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/registryHub")
    .withAutomaticReconnect()
    .build();

connection.on("ReceiveRegistryUpdate", (services) => renderServices(services));

async function start() {
    try {
        await connection.start();
        document.getElementById('lastSyncTime').innerText = "POŁĄCZONO LIVE";
        document.getElementById('lastSyncTime').className = "badge bg-success";
        fetchServicesState();
    } catch (err) {
        setTimeout(start, 5000);
    }
};

document.addEventListener('DOMContentLoaded', start);