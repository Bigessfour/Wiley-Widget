import { listControlsTool, implementControlTool } from '../src/tools/controls.mjs';

async function run() {
  console.log('Running listControlsTool...');
  const listResp = await listControlsTool.handler({});
  console.log('List response type:', Array.isArray(listResp.content) ? 'array' : typeof listResp);
  console.log('Sample item:', JSON.stringify(listResp.content[0]?.data?.[0] ?? listResp.content[0], null, 2));

  console.log('\nRunning implementControlTool for SfDataGrid...');
  const implResp = await implementControlTool.handler({ control: 'SfDataGrid' });
  console.log('Implement response content types:');
  implResp.content.forEach((c, idx) => {
    console.log(`  [${idx}] type=${c.type}${c.language ? ` language=${c.language}` : ''}`);
  });

  console.log('\nRunning implementControlTool for all controls...');
  const allResp = await implementControlTool.handler({ all: true });
  console.log('Implement all response is JSON array length:', allResp.content[0]?.data?.length ?? 0);

  console.log('\nAll tests passed (basic smoke tests).');
}

run().catch(e => { console.error(e); process.exit(1); });
