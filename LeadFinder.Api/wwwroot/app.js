// API Base URL
const API_BASE = '';

// State
let currentPage = 1;
let currentFilter = 'all';
let allResults = [];
let summary = null;

// Elements
const searchForm = document.getElementById('searchForm');
const searchBtn = document.getElementById('searchBtn');
const resultsContainer = document.getElementById('resultsContainer');
const statsDiv = document.getElementById('stats');
const filtersDiv = document.getElementById('filters');
const toast = document.getElementById('toast');
const toastMessage = document.getElementById('toastMessage');

// Event Listeners
searchForm.addEventListener('submit', handleSearch);

document.querySelectorAll('.filter-btn').forEach(btn => {
    btn.addEventListener('click', (e) => {
        document.querySelectorAll('.filter-btn').forEach(b => b.classList.remove('active'));
        e.target.classList.add('active');
        currentFilter = e.target.dataset.filter;
        renderResults();
    });
});

// Toast notification
function showToast(message, duration = 3000) {
    toastMessage.textContent = message;
    toast.classList.add('show');
    setTimeout(() => {
        toast.classList.remove('show');
    }, duration);
}

// Handle search form submission
async function handleSearch(e) {
    e.preventDefault();

    const city = document.getElementById('city').value.trim();
    const keyword = document.getElementById('keyword').value.trim();

    if (!city || !keyword) {
        showToast('⚠️ Lütfen şehir ve anahtar kelime girin');
        return;
    }

    searchBtn.disabled = true;
    searchBtn.textContent = '⏳ Aranıyor...';

    try {
        // Start scan
        const scanResponse = await fetch(`${API_BASE}/scan`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ city, keyword })
        });

        if (!scanResponse.ok) {
            throw new Error('Arama başlatılamadı');
        }

        showToast('✅ Arama başlatıldı, sonuçlar yükleniyor...');

        // Wait a bit for processing
        await new Promise(resolve => setTimeout(resolve, 3000));

        // Load results
        await loadResults();

    } catch (error) {
        console.error('Search error:', error);
        showToast('❌ Bir hata oluştu: ' + error.message);
    } finally {
        searchBtn.disabled = false;
        searchBtn.textContent = '🔍 Ara';
    }
}

// Load results from API
async function loadResults() {
    try {
        // Load summary
        const summaryResponse = await fetch(`${API_BASE}/results/summary`);
        summary = await summaryResponse.json();

        // Load all results
        const resultsResponse = await fetch(`${API_BASE}/results?pageSize=200`);
        const data = await resultsResponse.json();

        allResults = data.items || [];

        updateStats();
        renderResults();

        statsDiv.style.display = 'grid';
        filtersDiv.style.display = 'flex';

        showToast(`✅ ${allResults.length} sonuç bulundu!`);

    } catch (error) {
        console.error('Load results error:', error);
        showToast('❌ Sonuçlar yüklenemedi');
    }
}

// Update statistics
function updateStats() {
    if (!summary) return;

    document.getElementById('totalCount').textContent = summary.totalWebsites || 0;
    document.getElementById('okCount').textContent = summary.ok || 0;
    document.getElementById('poorCount').textContent = summary.poor || 0;
}

// Render results table
function renderResults() {
    if (allResults.length === 0) {
        resultsContainer.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="currentColor">
                    <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/>
                </svg>
                <h3>Sonuç bulunamadı</h3>
                <p>Farklı bir şehir veya anahtar kelime deneyin</p>
            </div>
        `;
        return;
    }

    // Filter results
    let filteredResults = allResults;
    if (currentFilter !== 'all') {
        filteredResults = allResults.filter(r => {
            if (currentFilter === 'ok') return r.quality === 'Ok';
            if (currentFilter === 'poor') return r.quality === 'Poor';
            return true;
        });
    }

    if (filteredResults.length === 0) {
        resultsContainer.innerHTML = `
            <div class="empty-state">
                <h3>Bu filtrede sonuç yok</h3>
                <p>Farklı bir filtre seçin</p>
            </div>
        `;
        return;
    }

    const html = `
        <table class="results-table">
            <thead>
                <tr>
                    <th>İşletme</th>
                    <th>Şehir</th>
                    <th>Web Sitesi</th>
                    <th>İletişim</th>
                    <th>Kalite</th>
                    <th>Skor</th>
                </tr>
            </thead>
            <tbody>
                ${filteredResults.map(result => `
                    <tr>
                        <td>
                            <strong>${escapeHtml(result.business || 'Bilinmiyor')}</strong>
                            ${result.address ? `<br><small style="color: #999;">${escapeHtml(result.address)}</small>` : ''}
                        </td>
                        <td>${escapeHtml(result.city || '')}</td>
                        <td>
                            ${result.homepageUrl ? `
                                <a href="${escapeHtml(result.homepageUrl)}" target="_blank" rel="noopener">${escapeHtml(result.domain || result.homepageUrl)}</a>
                            ` : '<span style="color: #999;">-</span>'}
                        </td>
                        <td class="contact-info">
                            ${formatContactInfo(result)}
                        </td>
                        <td>
                            <span class="quality-badge quality-${result.quality?.toLowerCase() || 'unknown'}">
                                ${result.quality || 'Unknown'}
                            </span>
                        </td>
                        <td>
                            <strong>${result.score !== null && result.score !== undefined ? result.score : '-'}</strong>
                        </td>
                    </tr>
                `).join('')}
            </tbody>
        </table>
    `;

    resultsContainer.innerHTML = html;
}

// Format contact information
function formatContactInfo(result) {
    const parts = [];

    // Email - check both business email and emails CSV
    const emails = [];
    if (result.email) emails.push(result.email);
    if (result.emails) {
        const csvEmails = result.emails.split(',').map(e => e.trim()).filter(e => e);
        csvEmails.forEach(e => {
            if (!emails.includes(e)) emails.push(e);
        });
    }

    if (emails.length > 0) {
        const uniqueEmails = [...new Set(emails)];
        parts.push(`📧 ${uniqueEmails.map(e => `<a href="mailto:${escapeHtml(e)}">${escapeHtml(e)}</a>`).join(', ')}`);
    }

    // Phone - check both business phone and phones CSV
    const phones = [];
    if (result.phone) phones.push(result.phone);
    if (result.phones) {
        const csvPhones = result.phones.split(',').map(p => p.trim()).filter(p => p);
        csvPhones.forEach(p => {
            if (!phones.includes(p)) phones.push(p);
        });
    }

    if (phones.length > 0) {
        const uniquePhones = [...new Set(phones)];
        parts.push(`📞 ${uniquePhones.map(p => `<a href="tel:${escapeHtml(p)}">${escapeHtml(p)}</a>`).join(', ')}`);
    }

    return parts.length > 0 ? parts.join('<br>') : '<span style="color: #999;">Bilgi yok</span>';
}

// Escape HTML
function escapeHtml(text) {
    if (!text) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Auto-load results on page load
window.addEventListener('load', async () => {
    try {
        await loadResults();
    } catch (error) {
        console.log('No existing results to load');
    }
});

// Auto-refresh every 10 seconds if there are results
setInterval(async () => {
    if (allResults.length > 0) {
        try {
            await loadResults();
        } catch (error) {
            console.error('Auto-refresh failed:', error);
        }
    }
}, 10000);