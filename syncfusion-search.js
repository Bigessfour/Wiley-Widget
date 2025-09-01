const https = require('https');

async function searchSyncfusion(query) {
    return new Promise((resolve, reject) => {
        const postData = JSON.stringify({
            query: 'site:help.syncfusion.com ' + query,
            limit: 10,
            country: 'US',
            language: 'en'
        });

        const options = {
            hostname: 'api.brightdata.com',
            port: 443,
            path: '/search',
            method: 'POST',
            headers: {
                'Authorization': 'Bearer a6192ce38ca78cc43169b81f2dc32301fa0f1381812726ce80d9d43ecbc5ffd3',
                'Content-Type': 'application/json',
                'Content-Length': Buffer.byteLength(postData)
            }
        };

        const req = https.request(options, (res) => {
            let data = '';

            res.on('data', (chunk) => {
                data += chunk;
            });

            res.on('end', () => {
                try {
                    const result = JSON.parse(data);
                    resolve(result);
                } catch (e) {
                    resolve({ error: 'Failed to parse response', raw: data });
                }
            });
        });

        req.on('error', (e) => {
            reject(e);
        });

        req.write(postData);
        req.end();
    });
}

async function main() {
    const queries = [
        'Syncfusion WPF 30.2.4 resources files',
        'Syncfusion WPF 30.2.4 theming resources',
        'Syncfusion WPF 30.2.4 SfSkinManager resources',
        'Syncfusion WPF 30.2.4 theme files location',
        'Syncfusion WPF 30.2.4 resource dictionary files'
    ];

    console.log('🔍 Searching Syncfusion documentation for resource conflicts...\n');

    for (const query of queries) {
        console.log(`Searching: ${query}`);
        try {
            const result = await searchSyncfusion(query);
            if (result.error) {
                console.log(`❌ Error: ${result.error}`);
            } else if (result.results && result.results.length > 0) {
                console.log(`✅ Found ${result.results.length} results:`);
                result.results.slice(0, 3).forEach((item, index) => {
                    console.log(`${index + 1}. ${item.title}`);
                    console.log(`   URL: ${item.url}`);
                    if (item.snippet) {
                        console.log(`   Summary: ${item.snippet.substring(0, 120)}...`);
                    }
                    console.log();
                });
            } else {
                console.log('❌ No results found');
            }
        } catch (error) {
            console.log(`❌ Request failed: ${error.message}`);
        }

        await new Promise(resolve => setTimeout(resolve, 2000));
    }

    console.log('🎉 Search completed!');
}

main().catch(console.error);
